using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class Team
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string LogoUrl { get; private set; } = string.Empty;
    public string Abbreviation { get; private set; } = string.Empty;

    private Team()
    {
    }

    public static Team Create(string name, string logoUrl, string abbreviation)
    {
        Validate(name, logoUrl, abbreviation);

        return new Team
        {
            Name = name,
            LogoUrl = logoUrl,
            Abbreviation = abbreviation
        };
    }

    public void UpdateDetails(string name, string logoUrl, string abbreviation)
    {
        Validate(name, logoUrl, abbreviation);

        Name = name;
        LogoUrl = logoUrl;
        Abbreviation = abbreviation;
    }

    private static void Validate(string name, string logoUrl, string abbreviation)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(logoUrl, nameof(logoUrl));
        Guard.Against.NullOrWhiteSpace(abbreviation, nameof(abbreviation));
        Guard.Against.LengthOutOfRange(abbreviation, 3, 3, nameof(abbreviation));
    }
}