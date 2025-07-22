using Dapper;
using PredictionLeague.Application.Data;

namespace PredictionLeague.Infrastructure.Data;

public class DapperReadDbConnection : IApplicationReadDbConnection
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperReadDbConnection(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(commandText: sql, parameters: param, cancellationToken: cancellationToken);

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<T>(command);
    }
    
    public async Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TReturn>(string sql, CancellationToken cancellationToken, Func<TFirst, TSecond, TReturn> map, object? param = null, string splitOn = "Id")
    {
        var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(command, map, splitOn);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(commandText: sql, parameters: param, cancellationToken: cancellationToken);

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<T>(command);
    }
}