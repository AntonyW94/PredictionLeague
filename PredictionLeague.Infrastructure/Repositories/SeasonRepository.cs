using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
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

    #region Create

    public async Task<Season> CreateAsync(Season season)
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
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var newSeasonId = await Connection.ExecuteScalarAsync<int>(sql, new
        {
            season.Name,
            season.StartDate,
            season.EndDate,
            season.IsActive
        });

        typeof(Season).GetProperty(nameof(Season.Id))?.SetValue(season, newSeasonId);
        return season;
    }

    #endregion

    #region Read

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

    #endregion

    #region Update

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

    #endregion
}