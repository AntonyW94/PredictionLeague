using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
using PredictionLeague.Shared.Admin.Rounds;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories
{
    public class RoundRepository : IRoundRepository
    {
        private readonly string _connectionString;

        public RoundRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<Round> AddAsync(Round round)
        {
            using var connection = Connection;
            const string sql = @"
                INSERT INTO [Rounds] 
                (
                    [SeasonId], 
                    [RoundNumber],
                    [StartDate],
                    [Deadline]
                )
                OUTPUT INSERTED.[Id]
                VALUES 
                (
                    @SeasonId, 
                    @RoundNumber,
                    @StartDate,
                    @Deadline
                );";

            var newId = await connection.QuerySingleAsync<int>(sql, round);
            round.Id = newId;
            return round;
        }

        public async Task<IEnumerable<RoundDto>> GetBySeasonIdAsync(int seasonId)
        {
            using var connection = Connection;
            const string sql = @"
                SELECT 
                    r.[Id],
                    r.[RoundNumber],
                    r.[StartDate],
                    r.[Deadline],
                    COUNT(m.[Id]) AS MatchCount
                FROM [Rounds] r
                LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
                WHERE r.[SeasonId] = @SeasonId
                GROUP BY r.[Id], r.[RoundNumber], r.[StartDate], r.[Deadline]
                ORDER BY r.[RoundNumber];";
            return await connection.QueryAsync<RoundDto>(sql, new { SeasonId = seasonId });
        }

        public async Task<Round?> GetCurrentRoundAsync(int seasonId)
        {
            using var connection = Connection;
            const string sql = @"
                SELECT TOP 1 * FROM [Rounds] 
                WHERE [SeasonId] = @SeasonId AND [Deadline] > GETUTCDATE() 
                ORDER BY [Deadline] ASC;";
            return await connection.QuerySingleOrDefaultAsync<Round>(sql, new { SeasonId = seasonId });
        }

        public async Task<Round?> GetByIdAsync(int id)
        {
            using var connection = Connection;
            const string sql = "SELECT * FROM [Rounds] WHERE [Id] = @Id;";
            return await connection.QuerySingleOrDefaultAsync<Round>(sql, new { Id = id });
        }

        public async Task UpdateAsync(Round round)
        {
            using var connection = Connection;
            const string sql = @"
        UPDATE [Rounds] SET
            [StartDate] = @StartDate,
            [Deadline] = @Deadline
        WHERE [Id] = @Id;";
            await connection.ExecuteAsync(sql, round);
        }
    }
}