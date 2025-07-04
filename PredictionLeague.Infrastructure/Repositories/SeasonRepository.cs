using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public SeasonRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Season>> GetAllAsync()
    {
        const string sql = "SELECT * FROM [Seasons] ORDER BY [StartDate] DESC;";

        using var connection = Connection;
        return await connection.QueryAsync<Season>(sql);
    }

    public async Task<Season?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM [Seasons] WHERE [Id] = @Id;";

        using var connection = Connection;
        return await connection.QuerySingleOrDefaultAsync<Season>(sql, new { Id = id });
    }

    public async Task AddAsync(Season season)
    {
        const string sql = @"
                INSERT INTO [Seasons]
                (
                    [Name],
                    [StartDate],
                    [EndDate],
                    [IsActive]
                )
                VALUES
                (
                    @Name,
                    @StartDate,
                    @EndDate,
                    @IsActive
                );";

        using var connection = Connection;
        await connection.ExecuteAsync(sql, season);
    }

    public async Task UpdateAsync(Season season)
    {
        const string sql = @"
                UPDATE [Seasons]
                SET
                    [Name] = @Name,
                    [StartDate] = @StartDate,
                    [EndDate] = @EndDate,
                    [IsActive] = @IsActive
                WHERE [Id] = @Id;";

        using var connection = Connection;
        await connection.ExecuteAsync(sql, season);
    }
}