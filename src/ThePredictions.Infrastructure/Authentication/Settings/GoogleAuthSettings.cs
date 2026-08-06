using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Infrastructure.Authentication.Settings;

[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
public class GoogleAuthSettings
{
    public const string SectionName = "Authentication:Google";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}