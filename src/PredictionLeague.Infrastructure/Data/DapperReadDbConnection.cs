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
   
    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(commandText: sql, parameters: param, cancellationToken: cancellationToken);

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<T>(command);
    }
}