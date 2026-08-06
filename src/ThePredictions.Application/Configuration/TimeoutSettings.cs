using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class TimeoutSettings
{
    public int FootballApiTimeoutSeconds { get; init; } = 30;
    public int EmailServiceTimeoutSeconds { get; init; } = 15;
    public int DatabaseCommandTimeoutSeconds { get; init; } = 30;
    public int DatabaseLongRunningCommandTimeoutSeconds { get; init; } = 120;
}
