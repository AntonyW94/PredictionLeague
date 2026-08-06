using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Competitions;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
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
