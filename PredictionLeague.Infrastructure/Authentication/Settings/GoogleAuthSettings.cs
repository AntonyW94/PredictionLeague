namespace PredictionLeague.Infrastructure.Authentication.Settings;

public class GoogleAuthSettings
{
    public const string SectionName = "Authentication:Google";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}