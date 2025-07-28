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
        INSERT INTO [Matches] ([RoundId], [HomeTeamId], [AwayTeamId], [MatchDateTime], [Status])
        VALUES (@RoundId, @HomeTeamId, @AwayTeamId, @MatchDateTime, @Status);";

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

    #endregion

    #region Update

    public async Task UpdateAsync(Round round, CancellationToken cancellationToken)
    {
        const string deleteMatchesSql = "DELETE FROM [Matches] WHERE [RoundId] = @RoundId;";
        const string updateRoundSql = @"
            UPDATE [Rounds]
            SET [RoundNumber] = @RoundNumber,
                [StartDate] = @StartDate,
                [Deadline] = @Deadline
            WHERE [Id] = @Id;";

        var deleteMatchesCommand = new CommandDefinition(
            commandText: deleteMatchesSql,
            parameters: new { RoundId = round.Id },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(deleteMatchesCommand);

        var updateRoundCommand = new CommandDefinition(
            commandText: updateRoundSql,
            parameters: round,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(updateRoundCommand);

        if (round.Matches.Any())
        {
            var matchesToInsert = round.Matches.Select(m => new
            {
                RoundId = round.Id,
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
                    matches
                );
            });

        return groupedResult.ToDictionary(r => r.Id);
    }

    #endregion
}