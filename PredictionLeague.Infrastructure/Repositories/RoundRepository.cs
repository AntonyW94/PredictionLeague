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
        INSERT INTO [dbo].[Matches] ([RoundId], [HomeTeamId], [AwayTeamId], [MatchDateTime], [Status])
        VALUES (@RoundId, @HomeTeamId, @AwayTeamId, @MatchDateTime, @Status);";

    private const string GetRoundsWithMatchesSql = @"
        SELECT 
            r.*, 
            m.*
        FROM [dbo].[Rounds] r
        LEFT JOIN [dbo].[Matches] m ON r.[Id] = m.[RoundId]";

    #endregion

    public RoundRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Round> CreateAsync(Round round)
    {
        const string sql = @"
            INSERT INTO [dbo].[Rounds] ([SeasonId], [RoundNumber], [StartDate], [Deadline])
            VALUES (@SeasonId, @RoundNumber, @StartDate, @Deadline);
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var newRoundId = await Connection.ExecuteScalarAsync<int>(sql, new
        {
            round.SeasonId,
            round.RoundNumber,
            round.StartDate,
            round.Deadline
        });

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

            await Connection.ExecuteAsync(AddMatchSql, matchesToInsert);
        }

        typeof(Round).GetProperty(nameof(Round.Id))?.SetValue(round, newRoundId);
        return round;
    }

    public async Task<IEnumerable<Round>> GetBySeasonIdAsync(int seasonId)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[SeasonId] = @SeasonId ORDER BY r.[RoundNumber];";
        var roundDictionary = await QueryAndMapRounds(sql, new { SeasonId = seasonId });
        return roundDictionary.Values;
    }
    public async Task<Round?> GetCurrentRoundAsync(int seasonId)
    {
        const string sql = @"
            SELECT TOP 1 r.*, m.*
            FROM [dbo].[Rounds] r
            LEFT JOIN [dbo].[Matches] m ON r.[Id] = m.[RoundId]
            WHERE r.[SeasonId] = @SeasonId AND r.[StartDate] > GETUTCDATE()
            ORDER BY r.[StartDate] ASC;";
        return await QueryAndMapRound(sql, new { SeasonId = seasonId });
    }

    public async Task<Round?> GetByIdAsync(int roundId)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Id] = @RoundId;";
        return await QueryAndMapRound(sql, new { RoundId = roundId });
    }

    public async Task UpdateAsync(Round round)
    {
        const string deleteMatchesSql = "DELETE FROM [dbo].[Matches] WHERE [RoundId] = @RoundId;";
        const string updateRoundSql = @"
            UPDATE [dbo].[Rounds]
            SET [RoundNumber] = @RoundNumber,
                [StartDate] = @StartDate,
                [Deadline] = @Deadline
            WHERE [Id] = @Id;";

        await Connection.ExecuteAsync(deleteMatchesSql, new { RoundId = round.Id });
        await Connection.ExecuteAsync(updateRoundSql, round);

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

            await Connection.ExecuteAsync(AddMatchSql, matchesToInsert);
        }
    }

    #region Private Helper Methods

    private async Task<Round?> QueryAndMapRound(string sql, object? param = null)
    {
        var roundDictionary = await QueryAndMapRounds(sql, param);
        return roundDictionary.Values.FirstOrDefault();
    }

    private async Task<Dictionary<int, Round>> QueryAndMapRounds(string sql, object? param = null)
    {
        var queryResult = await Connection.QueryAsync<Round, Match?, (Round Round, Match? Match)>(
            sql,
            (round, match) => (round, match),
            param,
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