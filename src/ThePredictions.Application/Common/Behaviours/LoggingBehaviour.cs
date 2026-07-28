using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ThePredictions.Application.Common.Behaviours;

/// <summary>
/// Records the outcome and duration of every command passing through the MediatR pipeline, so a
/// consumer action leaves a trail without each handler having to log one itself.
/// </summary>
public class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Commands only, matched on the naming convention in the root CLAUDE.md. Queries are
        // high-frequency reads and logging every one would bury the signal; reads that are
        // actually a problem are already reported by DapperReadDbConnection at Warning.
        if (!requestName.EndsWith("Command", StringComparison.Ordinal))
            return await next(cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);

            stopwatch.Stop();
            logger.LogInformation("{RequestName} completed in {ElapsedMilliseconds}ms", requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            // Information rather than Warning or Error on purpose. A rejected command is usually
            // the user's mistake - failed validation, a passed deadline - and logging it higher
            // would fill the alerts-warnings channel with things that need no action. Genuine
            // faults are still logged as Error by ErrorHandlingMiddleware further out.
            logger.LogInformation(
                "{RequestName} failed after {ElapsedMilliseconds}ms ({ExceptionType})",
                requestName,
                stopwatch.ElapsedMilliseconds,
                exception.GetType().Name);

            throw;
        }
    }
}
