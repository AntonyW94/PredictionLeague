using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
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
        const string sql = @"
        MERGE INTO [UserPredictions] AS target
        USING (SELECT @UserId AS UserId, @MatchId AS MatchId) AS source
        ON (target.[UserId] = source.[UserId] AND target.[MatchId] = source.[MatchId])
        WHEN MATCHED THEN
            UPDATE SET
                [PredictedHomeScore] = @PredictedHomeScore,
                [PredictedAwayScore] = @PredictedAwayScore,
                [UpdatedAtUtc] = @UpdatedAtUtc
        WHEN NOT MATCHED THEN
            INSERT ([MatchId], [UserId], [PredictedHomeScore], [PredictedAwayScore], [CreatedAtUtc], [UpdatedAtUtc], [Outcome])
            VALUES (@MatchId, @UserId, @PredictedHomeScore, @PredictedAwayScore, @CreatedAtUtc, @UpdatedAtUtc, @Outcome);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: predictions,
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
                [UserPredictions]
            SET
                [Outcome] = @Outcome,
                [UpdatedAtUtc] = GETUTCDATE()
            WHERE
                [Id] = @Id;";

        if (predictionsToUpdate.Any())
        {
            var command = new CommandDefinition(
                commandText: sql,
                parameters: predictionsToUpdate,
                transaction: Transaction,
                cancellationToken: cancellationToken
            );

            await Connection.ExecuteAsync(command);
        }

        await Task.CompletedTask;
    }

    #endregion
}
