using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories
{
    public class TeamRepository : ITeamRepository
    {
        private readonly string _connectionString;

        public TeamRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<IEnumerable<Team>> GetAllAsync()
        {
            using var connection = Connection;
            const string sql = @"
                SELECT 
                    [Id], 
                    [Name], 
                    [LogoUrl] 
                FROM [Teams] 
                ORDER BY [Name];";
            return await connection.QueryAsync<Team>(sql);
        }

        public async Task<Team> AddAsync(Team team)
        {
            using var connection = Connection;
            const string sql = @"
                INSERT INTO [Teams] 
                (
                    [Name], 
                    [LogoUrl]
                )
                OUTPUT INSERTED.*
                VALUES 
                (
                    @Name, 
                    @LogoUrl
                );";
            var newTeam = await connection.QuerySingleAsync<Team>(sql, team);
            return newTeam;
        }

        public async Task<Team?> GetByIdAsync(int id)
        {
            using var connection = Connection;
            const string sql = @"
                SELECT 
                    [Id], 
                    [Name], 
                    [LogoUrl] 
                FROM [Teams] 
                WHERE [Id] = @Id;";
            return await connection.QuerySingleOrDefaultAsync<Team>(sql, new { Id = id });
        }

        public async Task UpdateAsync(Team team)
        {
            using var connection = Connection;
            const string sql = @"
                UPDATE [Teams] 
                SET 
                    [Name] = @Name, 
                    [LogoUrl] = @LogoUrl 
                WHERE [Id] = @Id;";
            await connection.ExecuteAsync(sql, team);
        }
    }
}
