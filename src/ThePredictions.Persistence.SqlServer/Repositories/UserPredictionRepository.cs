using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class UserPredictionRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IUserPredictionRepository
{
    #region Create

    public Task UpsertBatchAsync(IEnumerable<UserPrediction> predictions, CancellationToken cancellationToken)
    {
        // Every fixture a player has just predicted, in one statement. This is the deadline-rush path - a whole
        // round submitted at once - so it used to be one round trip per fixture while the player waited.
        const string sql = @"
        MERGE INTO [UserPredictions] AS target
        USING (
            SELECT
                src.[MatchId],
                src.[UserId],
                src.[PredictedHomeScore],
                src.[PredictedAwayScore],
                src.[CreatedAtUtc],
                src.[UpdatedAtUtc],
                src.[Outcome]
            FROM
                OPENJSON(@Rows)
                WITH (
                    [MatchId] int 'strict $.MatchId',
                    [UserId] nvarchar(4000) 'strict $.UserId',
                    [PredictedHomeScore] int 'strict $.PredictedHomeScore',
                    [PredictedAwayScore] int 'strict $.PredictedAwayScore',
                    [CreatedAtUtc] datetime2 'strict $.CreatedAtUtc',
                    [UpdatedAtUtc] datetime2 'strict $.UpdatedAtUtc',
                    [Outcome] int 'strict $.Outcome'
                ) src
        ) AS source
        ON (target.[UserId] = source.[UserId] AND target.[MatchId] = source.[MatchId])
        WHEN MATCHED THEN
            UPDATE SET
                [PredictedHomeScore] = source.[PredictedHomeScore],
                [PredictedAwayScore] = source.[PredictedAwayScore],
                [UpdatedAtUtc] = source.[UpdatedAtUtc]
        WHEN NOT MATCHED THEN
            INSERT ([MatchId], [UserId], [PredictedHomeScore], [PredictedAwayScore], [CreatedAtUtc], [UpdatedAtUtc], [Outcome])
            VALUES (source.[MatchId], source.[UserId], source.[PredictedHomeScore], source.[PredictedAwayScore],
                    source.[CreatedAtUtc], source.[UpdatedAtUtc], source.[Outcome]);";

        // [Outcome] is an int column, so the enum is cast rather than left to the serialiser's default for
        // enums. Stating it means the stored value cannot change because a serialiser setting did.
        var rows = predictions
            .Select(prediction => new
            {
                prediction.MatchId,
                prediction.UserId,
                prediction.PredictedHomeScore,
                prediction.PredictedAwayScore,
                prediction.CreatedAtUtc,
                prediction.UpdatedAtUtc,
                Outcome = (int)prediction.Outcome
            })
            .ToList();

        if (rows.Count == 0)
            return Task.CompletedTask;

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Rows = JsonRows.From(rows) },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return Connection.ExecuteAsync(command);
    }

    #endregion

    #region Read

    public async Task<IEnumerable<UserPrediction>> GetByMatchIdsAsync(IEnumerable<int> matchIds, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                *
            FROM
                [UserPredictions]
            WHERE
                [MatchId] IN @MatchIds";

        return await Connection.QueryAsync<UserPrediction>(new CommandDefinition(sql, new { MatchIds = matchIds }, transaction: Transaction, cancellationToken: cancellationToken));
    }

    #endregion

    #region Update

    public async Task UpdateOutcomesAsync(IEnumerable<UserPrediction> predictionsToUpdate, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                up
            SET
                up.[Outcome] = src.[Outcome],

                -- The prediction's own timestamp, which UserPrediction.SetOutcome has already set from the injected clock.
                -- This used to be GETUTCDATE(), so the statement overwrote the entity's answer with the database's.
                up.[UpdatedAtUtc] = src.[UpdatedAtUtc]
            FROM
                [UserPredictions] up
            INNER JOIN
                OPENJSON(@Rows)
                WITH (
                    [Id] int 'strict $.Id',
                    [Outcome] int 'strict $.Outcome',
                    [UpdatedAtUtc] datetime2 'strict $.UpdatedAtUtc'
                ) src ON src.[Id] = up.[Id];";

        // [Outcome] is an int column - see the note in UpsertBatchAsync.
        var rows = predictionsToUpdate
            .Select(prediction => new
            {
                prediction.Id,
                Outcome = (int)prediction.Outcome,
                prediction.UpdatedAtUtc
            })
            .ToList();

        if (rows.Count == 0)
            return;

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Rows = JsonRows.From(rows) },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}
