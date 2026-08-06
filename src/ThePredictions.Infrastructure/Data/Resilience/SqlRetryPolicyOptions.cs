using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Infrastructure.Data.Resilience;

/// <summary>
/// Configuration options for the SQL transient-fault retry policy.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
public class SqlRetryPolicyOptions
{
    public const string SectionName = "SqlResilience";

    /// <summary>
    /// Maximum number of retry attempts before giving up.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Base delay in seconds for exponential back-off between retries.
    /// The actual delay is <c>BaseDelaySeconds ^ attempt</c> (i.e. 2s, 4s, 8s).
    /// </summary>
    public double BaseDelaySeconds { get; set; } = 2;
}
