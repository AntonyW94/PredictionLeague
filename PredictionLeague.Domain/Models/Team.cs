﻿using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class Team
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public string LogoUrl { get; private set; } = string.Empty;
    public string Abbreviation { get; private set; } = string.Empty;
    public int? ApiTeamId { get; private set; }

    private Team()
    {
    }

    public static Team Create(string name, string shortName, string logoUrl, string abbreviation, int? apiTeamId)
    {
        Validate(name, shortName, logoUrl, abbreviation);

        return new Team
        {
            Name = name,
            ShortName = shortName,
            LogoUrl = logoUrl,
            Abbreviation = abbreviation,
            ApiTeamId = apiTeamId
        };
    }

    public void UpdateDetails(string name, string shortName, string logoUrl, string abbreviation, int? apiTeamId)
    {
        Validate(name, shortName, logoUrl, abbreviation);

        Name = name;
        ShortName = shortName;
        LogoUrl = logoUrl;
        Abbreviation = abbreviation;
        ApiTeamId = apiTeamId;
    }

    private static void Validate(string name, string shortName, string logoUrl, string abbreviation)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(shortName, nameof(shortName));
        Guard.Against.NullOrWhiteSpace(logoUrl, nameof(logoUrl));
        Guard.Against.NullOrWhiteSpace(abbreviation, nameof(abbreviation));
        Guard.Against.LengthOutOfRange(abbreviation, 3, 3, nameof(abbreviation));
    }
}