using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
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

            int newId = await connection.QuerySingleAsync<int>(sql, round);
            round.Id = newId;
            return round;
        }
    }
}