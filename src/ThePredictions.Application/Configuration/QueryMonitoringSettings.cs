using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[ExcludeFromCodeCoverage]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class QueryMonitoringSettings
{
    // Read queries whose execution meets or exceeds this many milliseconds are logged at Warning
    // level, so slow database paths surface in the logs without a full APM setup. Default 500ms.
    public int SlowQueryThresholdMilliseconds { get; init; } = 500;
}
