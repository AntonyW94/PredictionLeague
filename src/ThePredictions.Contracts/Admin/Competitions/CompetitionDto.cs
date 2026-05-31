namespace ThePredictions.Contracts.Admin.Competitions;

public record CompetitionDto(
    int Id,
    string Code,
    string Name,
    int Type,
    string? LogoUrl,
    string? Description,
    int? ApiLeagueId,
    int SeasonCount
);
