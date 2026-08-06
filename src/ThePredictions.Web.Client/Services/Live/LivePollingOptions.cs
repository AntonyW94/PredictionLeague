using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Web.Client.Services.Live;

/// <summary>
/// Configuration for live-score polling. The interval is configurable (bound
/// from <c>LivePolling:IntervalSeconds</c> in configuration) and defaults to 10s.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Browser interop: a pass-through to JavaScript with no logic of its own.")]
public sealed class LivePollingOptions
{
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(10);
}
