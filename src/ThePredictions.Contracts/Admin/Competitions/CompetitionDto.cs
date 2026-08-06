using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Competitions;

[ExcludeFromCodeCoverage]
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
