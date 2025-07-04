using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public MatchRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(Match match)
    {
        const string sql = @"
                INSERT INTO [Matches] ([RoundId], [HomeTeamId], [AwayTeamId], [MatchDateTime], [Status])
                VALUES (@RoundId, @HomeTeamId, @AwayTeamId, @MatchDateTime, @Status);";

        using var dbConnection = Connection;
        await dbConnection.ExecuteAsync(sql, match);
    }

    public async Task<Match?> GetByIdAsync(int id)
    {
        const string sql = @"SELECT m.* FROM [Matches] m WHERE m.[Id] = @Id;";


        using var dbConnection = Connection;
        return await dbConnection.QuerySingleOrDefaultAsync<Match>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Match>> GetByRoundIdAsync(int roundId)
    {
        const string sql = @"
                SELECT
                    m.*,
                    ht.*,
                    at.*
                FROM [Matches] m
                INNER JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
                INNER JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
                WHERE m.[RoundId] = @RoundId
                ORDER BY m.[MatchDateTime];";

        using var connection = Connection;
        var matches = await connection.QueryAsync<Match, Team, Team, Match>(
            sql,
            (match, homeTeam, awayTeam) =>
            {
                match.HomeTeam = homeTeam;
                match.AwayTeam = awayTeam;
                return match;
            },
            new { RoundId = roundId },
            splitOn: "Id,Id"
        );
        return matches;
    }

    public async Task UpdateAsync(Match match)
    {
        const string sql = @"
                UPDATE [Matches]
                SET [RoundId] = @RoundId,
                    [HomeTeamId] = @HomeTeamId,
                    [AwayTeamId] = @AwayTeamId,
                    [MatchDateTime] = @MatchDateTime,
                    [Status] = @Status,
                    [ActualHomeTeamScore] = @ActualHomeTeamScore,
                    [ActualAwayTeamScore] = @ActualAwayTeamScore
                WHERE [Id] = @Id;";

        using var dbConnection = Connection;
        await dbConnection.ExecuteAsync(sql, match);
    }

    public async Task DeleteByRoundIdAsync(int roundId)
    {
        const string sql = "DELETE FROM [Matches] WHERE [RoundId] = @RoundId;";

        using var connection = Connection;
        await connection.ExecuteAsync(sql, new { RoundId = roundId });
    }
}