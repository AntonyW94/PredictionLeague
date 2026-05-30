using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

public class Competition
{
    public int Id { get; init; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public CompetitionType Type { get; private set; }
    public string? LogoUrl { get; private set; }
    public int? ApiLeagueId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public bool IsTournament => Type == CompetitionType.Tournament;

    private Competition()
    {
    }

    public Competition(int id, string code, string name, CompetitionType type, string? logoUrl, int? apiLeagueId, DateTime createdAtUtc)
    {
        Id = id;
        Code = code;
        Name = name;
        Type = type;
        LogoUrl = logoUrl;
        ApiLeagueId = apiLeagueId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Competition Create(string code, string name, CompetitionType type, string? logoUrl, int? apiLeagueId, IDateTimeProvider dateTimeProvider)
    {
        Validate(code, name);

        return new Competition
        {
            Code = code,
            Name = name,
            Type = type,
            LogoUrl = logoUrl,
            ApiLeagueId = apiLeagueId,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    public void UpdateDetails(string code, string name, CompetitionType type, string? logoUrl, int? apiLeagueId)
    {
        Validate(code, name);

        Code = code;
        Name = name;
        Type = type;
        LogoUrl = logoUrl;
        ApiLeagueId = apiLeagueId;
    }

    private static void Validate(string code, string name)
    {
        Guard.Against.NullOrWhiteSpace(code);
        Guard.Against.NullOrWhiteSpace(name);
    }
}
