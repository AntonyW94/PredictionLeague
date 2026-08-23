using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class QueryMonitoringSettings
{
    // Read queries whose execution meets or exceeds this many milliseconds are logged at Warning
    // level, so slow database paths surface in the logs without a full APM setup. Default 500ms.
    public int SlowQueryThresholdMilliseconds { get; init; } = 500;

    // Transactional commands whose transaction stays open for at least this many milliseconds are
    // logged at Warning level. With READ_COMMITTED_SNAPSHOT off, a reader waits for whichever writer
    // holds the rows it wants, so the duration a transaction is held open is the duration unrelated
    // reads can be blocked for - which makes it the number to watch, not just the command's own cost.
    // Deliberately higher than the read threshold: a write transaction is expected to be slower than
    // a read, and the interesting case is one held open far longer than the reads it delays.
    public int SlowTransactionThresholdMilliseconds { get; init; } = 1000;
}
