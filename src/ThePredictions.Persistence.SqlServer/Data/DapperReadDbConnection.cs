using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;

namespace ThePredictions.Persistence.SqlServer.Data;

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
        catch (InvalidOperationException exception)
        {
            // Dapper reports a result-set/result-record mismatch ("A parameterless default constructor or
            // one matching signature ... is required for ... materialization") and a single-row query that
            // returned multiple rows as a plain InvalidOperationException. Wrapping it names the failure in
            // the log message instead of leaving a bare Dapper exception to be interpreted.
            //
            // This no longer decides the severity: InvalidOperationException is itself reported as an Error
            // and a 500 (see ADR-0016), so an untranslated read fault is filed correctly regardless.
            throw new ReadQueryFailedException(exception);
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds >= _slowQueryThresholdMilliseconds)
                logger.LogWarning("Slow query ({ElapsedMilliseconds}ms >= {ThresholdMilliseconds}ms threshold): {Sql}", stopwatch.ElapsedMilliseconds, _slowQueryThresholdMilliseconds, sql);
        }
    }
}
