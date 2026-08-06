using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using ThePredictions.Application.Configuration;
using System.Net;

namespace ThePredictions.Infrastructure.Resilience;

[ExcludeFromCodeCoverage(Justification = "Polly pipeline configuration: declarative retry, circuit-breaker and timeout wiring with no logic of its own, verified against the live API.")]
public static class FootballApiResilienceConfiguration
{
    public static void Configure(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext context)
    {
        var settings = context.ServiceProvider
            .GetRequiredService<IOptions<FootballApiResilienceSettings>>().Value;

        builder
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = settings.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(settings.MedianFirstRetryDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r =>
                        r.StatusCode == HttpStatusCode.TooManyRequests
                        || r.StatusCode == HttpStatusCode.RequestTimeout
                        || (int)r.StatusCode >= 500)
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>(),
                OnRetry = args =>
                {
                    var logger = context.ServiceProvider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("ThePredictions.Infrastructure.Resilience.FootballApi");

                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Football API retry attempt {AttemptNumber} of {MaxRetryAttempts} after {RetryDelayMs}ms. Status code: {StatusCode}",
                        args.AttemptNumber + 1,
                        settings.MaxRetryAttempts,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode);

                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = settings.CircuitBreakerFailureThreshold,
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerBreakDurationSeconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r =>
                        r.StatusCode == HttpStatusCode.TooManyRequests
                        || r.StatusCode == HttpStatusCode.RequestTimeout
                        || (int)r.StatusCode >= 500)
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>(),
                OnOpened = args =>
                {
                    var logger = context.ServiceProvider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("ThePredictions.Infrastructure.Resilience.FootballApi");

                    logger.LogError(
                        args.Outcome.Exception,
                        "Football API circuit breaker opened for {BreakDurationSeconds}s due to repeated failures. Status code: {StatusCode}",
                        settings.CircuitBreakerBreakDurationSeconds,
                        args.Outcome.Result?.StatusCode);

                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    var logger = context.ServiceProvider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("ThePredictions.Infrastructure.Resilience.FootballApi");

                    logger.LogInformation("Football API circuit breaker closed. Service recovered");

                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    var logger = context.ServiceProvider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("ThePredictions.Infrastructure.Resilience.FootballApi");

                    logger.LogInformation("Football API circuit breaker half-opened. Testing service availability");

                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));
    }
}
