using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Shared.Admin.Seasons;
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
        const string sql = "INSERT INTO [Seasons] ([Name], [StartDate], [EndDate], [IsActive]) VALUES (@Name, @StartDate, @EndDate, @IsActive);";
        await dbConnection.ExecuteAsync(sql, season);
    }

    public async Task<IEnumerable<SeasonDto>> GetAllAsync()
    {
        using var connection = Connection;
        const string sql = @"
                SELECT 
                    s.[Id],
                    s.[Name],
                    s.[StartDate],
                    s.[EndDate],
                    s.[IsActive],
                    COUNT(r.[Id]) AS RoundCount
                FROM [Seasons] s
                LEFT JOIN [Rounds] r ON s.[Id] = r.[SeasonId]
                GROUP BY s.[Id], s.[Name], s.[StartDate], s.[EndDate], s.[IsActive]
                ORDER BY s.[StartDate] DESC;";
        return await connection.QueryAsync<SeasonDto>(sql);
    }

    public async Task<SeasonDto?> GetByIdAsync(int id)
    {
        using var connection = Connection;
        const string sql = @"
                SELECT 
                    s.[Id],
                    s.[Name],
                    s.[StartDate],
                    s.[EndDate],
                    s.[IsActive],
                    COUNT(r.[Id]) AS RoundCount
                FROM [Seasons] s
                LEFT JOIN [Rounds] r ON s.[Id] = r.[SeasonId]
                WHERE s.[Id] = @Id
                GROUP BY s.[Id], s.[Name], s.[StartDate], s.[EndDate], s.[IsActive];";
        return await connection.QuerySingleOrDefaultAsync<SeasonDto>(sql, new { Id = id });
    }

    public async Task UpdateAsync(int id, UpdateSeasonRequest request)
    {
        using var connection = Connection;
        const string sql = @"
                UPDATE [Seasons] SET
                    [Name] = @Name,
                    [StartDate] = @StartDate,
                    [EndDate] = @EndDate,
                    [IsActive] = @IsActive
                WHERE [Id] = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id, request.Name, request.StartDate, request.EndDate, request.IsActive });
    }
}