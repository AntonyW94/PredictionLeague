using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using ThePredictions.Application.Data;

namespace ThePredictions.Infrastructure.Data.Resilience;

/// <summary>
/// Wraps database operations with a Polly retry pipeline that handles transient SQL failures
/// using exponential back-off.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Database plumbing: connection, transaction and type-handler wiring with no branching logic of its own.")]
public class SqlRetryPolicy : ISqlRetryPolicy
{
    private readonly ResiliencePipeline _pipeline;

    public SqlRetryPolicy(IOptions<SqlRetryPolicyOptions> options, ILogger<SqlRetryPolicy> logger)
    {
        var config = options.Value;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(SqlTransientFaultDetector.IsTransient),
                MaxRetryAttempts = config.MaxRetryCount,
                Delay = TimeSpan.FromSeconds(config.BaseDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient SQL failure (attempt {AttemptNumber} of {MaxRetryCount}). Retrying in {RetryDelay}",
                        args.AttemptNumber + 1,
                        config.MaxRetryCount,
                        args.RetryDelay);

                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(
            async ct => await operation(ct),
            cancellationToken);
    }
}
