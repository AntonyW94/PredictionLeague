using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;

namespace ThePredictions.Infrastructure.Data;

public class DapperReadDbConnection(
    IDbConnectionFactory connectionFactory,
    ISqlRetryPolicy retryPolicy,
    IOptions<TimeoutSettings> timeoutSettings,
    IOptions<QueryMonitoringSettings> queryMonitoringSettings,
    ILogger<DapperReadDbConnection> logger) : IApplicationReadDbConnection
{
    private readonly int _commandTimeout = timeoutSettings.Value.DatabaseCommandTimeoutSeconds;
    private readonly int _slowQueryThresholdMilliseconds = queryMonitoringSettings.Value.SlowQueryThresholdMilliseconds;

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken, object? param = null)
    {
        return await ExecuteTimedAsync(sql, async ct =>
        {
            var command = new CommandDefinition(commandText: sql, parameters: param, cancellationToken: ct, commandTimeout: _commandTimeout);

            using var connection = connectionFactory.CreateConnection();
            return await connection.QueryAsync<T>(command);
        }, cancellationToken);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken, object? param = null)
    {
        return await ExecuteTimedAsync(sql, async ct =>
        {
            var command = new CommandDefinition(commandText: sql, parameters: param, cancellationToken: ct, commandTimeout: _commandTimeout);

            using var connection = connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(command);
        }, cancellationToken);
    }

    // Runs the (retried) read and logs a Warning when it takes at least the configured threshold, so
    // slow query paths are visible in the logs. Parameters are never logged - the SQL is parameterised,
    // so the text carries no user data. Timing wraps the whole retried execution (retry overhead counts
    // as slow too); the finally block ensures a slow failure is still reported.
    private async Task<TResult> ExecuteTimedAsync<TResult>(string sql, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await retryPolicy.ExecuteAsync(operation, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds >= _slowQueryThresholdMilliseconds)
                logger.LogWarning("Slow query ({ElapsedMilliseconds}ms >= {ThresholdMilliseconds}ms threshold): {Sql}", stopwatch.ElapsedMilliseconds, _slowQueryThresholdMilliseconds, sql);
        }
    }
}
