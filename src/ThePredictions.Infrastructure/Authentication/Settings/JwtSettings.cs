using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Infrastructure.Authentication.Settings;

[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public double ExpiryMinutes { get; init; }
    public double RefreshTokenExpiryDays { get; init; }
}