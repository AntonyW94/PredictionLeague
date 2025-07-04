using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories
{
    public class MatchRepository : IMatchRepository
    {
        private readonly string _connectionString;

        public MatchRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task AddAsync(Match match)
        {
            using var dbConnection = Connection;
            var sql = @"
                INSERT INTO [Matches] ([RoundId], [HomeTeamId], [AwayTeamId], [MatchDateTime], [Status])
                VALUES (@RoundId, @HomeTeamId, @AwayTeamId, @MatchDateTime, @Status);";
            await dbConnection.ExecuteAsync(sql, match);
        }

        public async Task<Match?> GetByIdAsync(int id)
        {
            using var dbConnection = Connection;
            const string sql = @"SELECT m.* FROM [Matches] m WHERE m.[Id] = @Id;";
            
            return await dbConnection.QuerySingleOrDefaultAsync<Match>(sql, new { Id = id });
        }

        public async Task<IEnumerable<Match>> GetByRoundIdAsync(int roundId)
        {
            using var connection = Connection;
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

            var matches = await connection.QueryAsync<Match, Team, Team, Match>(
                sql,
                (match, homeTeam, awayTeam) =>
                {
                    match.HomeTeam = homeTeam;
                    match.AwayTeam = awayTeam;
                    return match;
                },
                new { RoundId = roundId },
                splitOn: "Id,Id" // Split on the "Id" column for the home and away teams
            );
            return matches;
        }

        public async Task UpdateAsync(Match match)
        {
            using var dbConnection = Connection;
            var sql = @"
                UPDATE [Matches]
                SET [RoundId] = @RoundId,
                    [HomeTeamId] = @HomeTeamId,
                    [AwayTeamId] = @AwayTeamId,
                    [MatchDateTime] = @MatchDateTime,
                    [Status] = @Status,
                    [ActualHomeTeamScore] = @ActualHomeTeamScore,
                    [ActualAwayTeamScore] = @ActualAwayTeamScore
                WHERE [Id] = @Id;";
            
            await dbConnection.ExecuteAsync(sql, match);
        }

        public async Task DeleteByRoundIdAsync(int roundId)
        {
            using var connection = Connection;
            const string sql = "DELETE FROM [Matches] WHERE [RoundId] = @RoundId;";
            await connection.ExecuteAsync(sql, new { RoundId = roundId });
        }
    }
}
