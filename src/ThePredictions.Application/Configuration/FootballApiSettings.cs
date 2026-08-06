using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
public class FootballApiSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
}