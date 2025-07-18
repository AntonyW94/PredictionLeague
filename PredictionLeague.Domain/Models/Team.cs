using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class Team
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string LogoUrl { get; private set; } = string.Empty;

    private Team() { }

    public static Team Create(string name, string logoUrl)
    {
        Validate(name, logoUrl);

        return new Team
        {
            Name = name,
            LogoUrl = logoUrl
        };
    }

    public void UpdateDetails(string name, string logoUrl)
    {
        Validate(name, logoUrl);

        Name = name;
        LogoUrl = logoUrl;
    }

    private static void Validate(string name, string logoUrl)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(logoUrl, nameof(logoUrl));
    }
}