using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class RoundRepository : IRoundRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    #region SQL Constants

    private const string AddMatchSql = @"
        INSERT INTO [Matches] 
        (
            [RoundId], 
            [HomeTeamId], 
            [AwayTeamId], 
            [MatchDateTime], 
            [Status]
        )
        VALUES 
        (
            @RoundId, 
            @HomeTeamId, 
            @AwayTeamId, 
            @MatchDateTime, 
            @Status
        );";
    
    private const string GetRoundsWithMatchesSql = @"
        SELECT 
            r.*, 
            m.*
        FROM [Rounds] r
        LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]";

    #endregion

    public RoundRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Create

    public async Task<Round> CreateAsync(Round round, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [Rounds] ([SeasonId], [RoundNumber], [StartDate], [Deadline])
            VALUES (@SeasonId, @RoundNumber, @StartDate, @Deadline);
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                round.SeasonId,
                round.RoundNumber,
                round.StartDate,
                round.Deadline
            },
            cancellationToken: cancellationToken
        );

        var newRoundId = await Connection.ExecuteScalarAsync<int>(command);

        if (round.Matches.Any())
        {
            var matchesToInsert = round.Matches.Select(m => new
            {
                RoundId = newRoundId,
                m.HomeTeamId,
                m.AwayTeamId,
                m.MatchDateTime,
                Status = m.Status.ToString()
            }).ToList();
         
            var insertMatchesCommand = new CommandDefinition(
                commandText: AddMatchSql,
                parameters: matchesToInsert,
                cancellationToken: cancellationToken
            );
            
            await Connection.ExecuteAsync(insertMatchesCommand);
        }

        typeof(Round).GetProperty(nameof(Round.Id))?.SetValue(round, newRoundId);
        return round;
    }

    #endregion

    #region Read

    public async Task<Round?> GetByIdAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Id] = @RoundId;";
        return await QueryAndMapRound(sql, cancellationToken, new { RoundId = roundId });
    }

    public async Task<IEnumerable<int>> GetMatchIdsWithPredictionsAsync(IEnumerable<int> matchIds)
    {
        const string sql = @"
            SELECT DISTINCT
                [MatchId]
            FROM
                [UserPredictions]
            WHERE
                [MatchId] IN @MatchIds;
        ";

        var matchIdsList = matchIds.ToList();
        if (!matchIdsList.Any())
            return Enumerable.Empty<int>();
        
        return await Connection.QueryAsync<int>(sql, new { MatchIds = matchIdsList });
    }

    #endregion

    #region Update

    public async Task UpdateAsync(Round round, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string updateRoundSql = @"
            UPDATE 
                [Rounds]
            SET 
                [RoundNumber] = @RoundNumber,
                [StartDate] = @StartDate,
                [Deadline] = @Deadline,
                [Status] = @Status
            WHERE 
                [Id] = @Id;";

            var updateRoundCommand = new CommandDefinition(updateRoundSql, new
            {
                round.Id,
                round.RoundNumber,
                round.StartDate,
                round.Deadline,
                Status = round.Status.ToString()
            }, transaction, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(updateRoundCommand);

            var existingMatchIdsCommand = new CommandDefinition("SELECT [Id] FROM [Matches] WHERE [RoundId] = @RoundId", new { RoundId = round.Id }, transaction, cancellationToken: cancellationToken);
            var existingMatchIds = (await connection.QueryAsync<int>(existingMatchIdsCommand)).ToList();
            var incomingMatches = round.Matches.ToList();

            var matchesToInsert = incomingMatches.Where(m => m.Id == 0).ToList();
            var matchesToUpdate = incomingMatches.Where(m => m.Id != 0).ToList();
            var matchIdsToDelete = existingMatchIds.Except(incomingMatches.Select(m => m.Id)).ToList();

            if (matchesToInsert.Any())
            {
                var insertMatchesCommand = new CommandDefinition(AddMatchSql, matchesToInsert.Select(m => new {
                    RoundId = round.Id,
                    m.HomeTeamId,
                    m.AwayTeamId,
                    m.MatchDateTime,
                    Status = m.Status.ToString()
                }), transaction, cancellationToken: cancellationToken);
                await connection.ExecuteAsync(insertMatchesCommand);
            }

            if (matchesToUpdate.Any())
            {
                const string updateSql = @"
                UPDATE 
                    [Matches]
                SET
                    [HomeTeamId] = @HomeTeamId,
                    [AwayTeamId] = @AwayTeamId,
                    [MatchDateTime] = @MatchDateTime
                WHERE
                    [Id] = @Id;";

                var updateMatchesCommand = new CommandDefinition(updateSql, matchesToUpdate, transaction, cancellationToken: cancellationToken);
                await connection.ExecuteAsync(updateMatchesCommand);
            }

            if (matchIdsToDelete.Any())
            {
                const string deleteSql = "DELETE FROM [Matches] WHERE [Id] IN @MatchIdsToDelete;";
                var deleteMatchesCommand = new CommandDefinition(deleteSql, new { MatchIdsToDelete = matchIdsToDelete }, transaction, cancellationToken: cancellationToken);
                await connection.ExecuteAsync(deleteMatchesCommand);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateMatchScoresAsync(List<Match> matches, CancellationToken cancellationToken)
    {
        if (!matches.Any())
            return;
        
        const string sql = @"
        UPDATE
            [Matches]
        SET
            [ActualHomeTeamScore] = @ActualHomeTeamScore,
            [ActualAwayTeamScore] = @ActualAwayTeamScore,
            [Status] = @Status
        WHERE
            [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: matches.Select(m => new {
                m.Id,
                m.ActualHomeTeamScore,
                m.ActualAwayTeamScore,
                Status = m.Status.ToString()
            }),
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Private Helper Methods

    private async Task<Round?> QueryAndMapRound(string sql, CancellationToken cancellationToken, object? param = null)
    {
        return (await QueryAndMapRounds(sql, cancellationToken, param)).Values.FirstOrDefault();
    }

    private async Task<Dictionary<int, Round>> QueryAndMapRounds(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(
            commandText: sql,
            parameters: param,
            cancellationToken: cancellationToken
        );
        
        var queryResult = await Connection.QueryAsync<Round, Match?, (Round Round, Match? Match)>(
            command,
            (round, match) => (round, match),
            splitOn: "Id"
        );

        var groupedResult = queryResult
            .GroupBy(p => p.Round.Id)
            .Select(g =>
            {
                var groupedRound = g.First().Round;
                var matches = g.Select(p => p.Match).Where(m => m != null).ToList();

                return new Round(
                    groupedRound.Id,
                    groupedRound.SeasonId,
                    groupedRound.RoundNumber,
                    groupedRound.StartDate,
                    groupedRound.Deadline,
                    groupedRound.Status,
                    matches
                );
            });

        return groupedResult.ToDictionary(r => r.Id);
    }

    #endregion
}