using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class Team
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string LogoUrl { get; private set; } = string.Empty;

    private Team() { }

    public static Team Create(string name, string? logoUrl)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(Name));
        Guard.Against.NullOrWhiteSpace(logoUrl, nameof(LogoUrl));

        return new Team
        {
            Name = name,
            LogoUrl = logoUrl
        };
    }

    public void UpdateDetails(string newName, string? newLogoUrl)
    {
        Guard.Against.NullOrWhiteSpace(newName, nameof(Name));
        Guard.Against.NullOrWhiteSpace(newLogoUrl, nameof(LogoUrl));

        Name = newName;
        LogoUrl = newLogoUrl;
    }
}