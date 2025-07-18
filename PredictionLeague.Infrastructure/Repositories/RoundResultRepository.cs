using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class RoundResultRepository : IRoundResultRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public RoundResultRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task UpsertAsync(RoundResult result)
    {
        const string sql = @"
                MERGE INTO [RoundResults] AS t
                USING (VALUES (@RoundId, @UserId)) AS s ([RoundId], [UserId])
                ON t.[RoundId] = s.[RoundId] AND t.[UserId] = s.[UserId]
                WHEN MATCHED THEN
                    UPDATE SET [TotalPoints] = @TotalPoints
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT ([RoundId], [UserId], [TotalPoints])
                    VALUES (@RoundId, @UserId, @TotalPoints);";

        using var dbConnection = Connection;
        await dbConnection.ExecuteAsync(sql, result);
    }
}