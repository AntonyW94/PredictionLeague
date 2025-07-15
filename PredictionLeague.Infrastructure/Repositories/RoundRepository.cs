using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class RoundRepository : IRoundRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public RoundRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Round> AddAsync(Round round)
    {
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

        using var connection = Connection;
        var newId = await connection.QuerySingleAsync<int>(sql, round);
        round.Id = newId;
        return round;
    }

    public async Task<IEnumerable<Round>> GetBySeasonIdAsync(int seasonId)
    {
        const string sql = "SELECT * FROM [Rounds] WHERE [SeasonId] = @SeasonId ORDER BY [RoundNumber];";

        using var connection = Connection;
        return await connection.QueryAsync<Round>(sql, new { SeasonId = seasonId });
    }

    public async Task<Round?> GetCurrentRoundAsync(int seasonId)
    {
        const string sql = @"
                SELECT TOP 1 * FROM [Rounds] 
                WHERE [SeasonId] = @SeasonId AND [Deadline] > GETUTCDATE() 
                ORDER BY [Deadline] ASC;";

        using var connection = Connection;
        return await connection.QuerySingleOrDefaultAsync<Round>(sql, new { SeasonId = seasonId });
    }

    public async Task<Round?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM [Rounds] WHERE [Id] = @Id;";

        using var connection = Connection;
        return await connection.QuerySingleOrDefaultAsync<Round>(sql, new { Id = id });
    }

    public async Task UpdateAsync(Round round)
    {
        const string sql = @"
                UPDATE [Rounds] 
                SET [StartDate] = @StartDate,
                    [Deadline] = @Deadline
                WHERE [Id] = @Id;";
        using var connection = Connection;

        await connection.ExecuteAsync(sql, round);
    }
}