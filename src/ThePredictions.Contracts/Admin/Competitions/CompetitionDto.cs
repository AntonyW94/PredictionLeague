namespace ThePredictions.Contracts.Admin.Competitions;

public record CompetitionDto(
    int Id,
    string Code,
    string Name,
    int Type,
    string? LogoUrl,
    int? ApiLeagueId,
    int SeasonCount
);
