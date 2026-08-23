using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;

namespace ThePredictions.Application.Common.Behaviours;

public class TransactionBehaviour<TRequest, TResponse>(
    IDbTransactionContext transactionContext,
    IOptions<QueryMonitoringSettings> queryMonitoringSettings,
    ILogger<TransactionBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>, ITransactionalRequest
{
    private readonly int _slowTransactionThresholdMilliseconds = queryMonitoringSettings.Value.SlowTransactionThresholdMilliseconds;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        await transactionContext.BeginAsync(cancellationToken);

        // Timed from before the handler runs to after the commit returns, because that whole span is
        // how long the transaction's locks are held - and with READ_COMMITTED_SNAPSHOT off, that is
        // also how long an unrelated read touching the same rows can be made to wait. A command that
        // does slow work between its first write and its commit (an HTTP call, an email send) blocks
        // readers for the duration whether or not any individual statement in it was slow, which the
        // per-statement slow-query warning cannot show. Reported at Warning next to those warnings so
        // the two can be lined up by timestamp.
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogDebug("Beginning transaction for {RequestName}", requestName);

            var response = await next(cancellationToken);

            await transactionContext.CommitAsync(cancellationToken);

            logger.LogDebug("Committed transaction for {RequestName}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transaction for {RequestName} failed. Rolling back.", requestName);
            throw;
        }
        finally
        {
            // In the finally rather than after the commit, so a transaction that was held open and
            // then failed is reported too - that one blocked readers for just as long.
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds >= _slowTransactionThresholdMilliseconds)
                logger.LogWarning(
                    "Slow transaction: {RequestName} held a transaction open for {ElapsedMilliseconds}ms (>= {ThresholdMilliseconds}ms threshold), blocking reads of the rows it wrote",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    _slowTransactionThresholdMilliseconds);
        }
    }
}
