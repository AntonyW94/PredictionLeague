using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly string _connectionString;

    public SeasonRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection Connection => new SqlConnection(_connectionString);

    public async Task AddAsync(Season season)
    {
        using var dbConnection = Connection;
        const string sql = "INSERT INTO [Seasons] ([YearName], [StartDate], [EndDate], [IsActive]) VALUES (@YearName, @StartDate, @EndDate, @IsActive);";
        await dbConnection.ExecuteAsync(sql, season);
    }

    public async Task<Season?> GetActiveAsync()
    {
        using var dbConnection = Connection;
        const string sql = "SELECT s.* FROM [Seasons] gy WHERE s.[IsActive] = 1;";
        return await dbConnection.QuerySingleOrDefaultAsync<Season>(sql);
    }

    public async Task<IEnumerable<Season>> GetAllAsync()
    {
        using var dbConnection = Connection;
        const string sql = "SELECT s.* FROM [Seasons] gy ORDER BY s.[StartDate] DESC;";
        return await dbConnection.QueryAsync<Season>(sql);
    }

    public async Task<Season?> GetByIdAsync(int id)
    {
        using var dbConnection = Connection;
        const string sql = "SELECT s.* FROM [Seasons] gy WHERE s.[Id] = @Id;";
        return await dbConnection.QuerySingleOrDefaultAsync<Season>(sql, new { Id = id });
    }

    public async Task UpdateAsync(Season season)
    {
        using var dbConnection = Connection;
        const string sql = @"
                UPDATE [Seasons]
                SET [Name] = @Name,
                    [StartDate] = @StartDate,
                    [EndDate] = @EndDate,
                    [IsActive] = @IsActive
                WHERE [Id] = @Id;";
        await dbConnection.ExecuteAsync(sql, season);
    }
}