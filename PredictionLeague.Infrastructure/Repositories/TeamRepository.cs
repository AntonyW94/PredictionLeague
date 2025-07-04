using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public TeamRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Team>> GetAllAsync()
    {
        const string sql = @"
                SELECT 
                    [Id], 
                    [Name], 
                    [LogoUrl] 
                FROM [Teams] 
                ORDER BY [Name];";

        using var connection = Connection;
        return await connection.QueryAsync<Team>(sql);
    }

    public async Task<Team> AddAsync(Team team)
    {
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

        using var connection = Connection;
        return await connection.QuerySingleAsync<Team>(sql, team);
    }

    public async Task<Team?> GetByIdAsync(int id)
    {
        const string sql = @"
                SELECT 
                    [Id], 
                    [Name], 
                    [LogoUrl] 
                FROM [Teams] 
                WHERE [Id] = @Id;";

        using var connection = Connection;
        return await connection.QuerySingleOrDefaultAsync<Team>(sql, new { Id = id });
    }

    public async Task UpdateAsync(Team team)
    {
        const string sql = @"
                UPDATE [Teams] 
                SET 
                    [Name] = @Name, 
                    [LogoUrl] = @LogoUrl 
                WHERE [Id] = @Id;";

        using var connection = Connection;
        await connection.ExecuteAsync(sql, team);
    }
}