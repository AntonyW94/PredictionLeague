using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class GameYearRepository : IGameYearRepository
{
    private readonly string _connectionString;

    public GameYearRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection Connection => new SqlConnection(_connectionString);

    public async Task AddAsync(GameYear gameYear)
    {
        using var dbConnection = Connection;
        const string sql = "INSERT INTO [GameYears] ([YearName], [StartDate], [EndDate], [IsActive]) VALUES (@YearName, @StartDate, @EndDate, @IsActive);";
        await dbConnection.ExecuteAsync(sql, gameYear);
    }

    public async Task<GameYear?> GetActiveAsync()
    {
        using var dbConnection = Connection;
        const string sql = "SELECT gy.* FROM [GameYears] gy WHERE gy.[IsActive] = 1;";
        return await dbConnection.QuerySingleOrDefaultAsync<GameYear>(sql);
    }

    public async Task<IEnumerable<GameYear>> GetAllAsync()
    {
        using var dbConnection = Connection;
        const string sql = "SELECT gy.* FROM [GameYears] gy ORDER BY gy.[StartDate] DESC;";
        return await dbConnection.QueryAsync<GameYear>(sql);
    }

    public async Task<GameYear?> GetByIdAsync(int id)
    {
        using var dbConnection = Connection;
        const string sql = "SELECT gy.* FROM [GameYears] gy WHERE gy.[Id] = @Id;";
        return await dbConnection.QuerySingleOrDefaultAsync<GameYear>(sql, new { Id = id });
    }

    public async Task UpdateAsync(GameYear gameYear)
    {
        using var dbConnection = Connection;
        const string sql = @"
                UPDATE [GameYears]
                SET [YearName] = @YearName,
                    [StartDate] = @StartDate,
                    [EndDate] = @EndDate,
                    [IsActive] = @IsActive
                WHERE [Id] = @Id;";
        await dbConnection.ExecuteAsync(sql, gameYear);
    }
}