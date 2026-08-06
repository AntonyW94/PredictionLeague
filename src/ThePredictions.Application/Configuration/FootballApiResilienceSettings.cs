using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class FootballApiResilienceSettings
{
    public int MaxRetryAttempts { get; init; } = 3;
    public int MedianFirstRetryDelaySeconds { get; init; } = 2;
    public int CircuitBreakerFailureThreshold { get; init; } = 5;
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;
    public int RequestTimeoutSeconds { get; init; } = 30;
}
