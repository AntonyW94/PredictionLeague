using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Teams;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record TeamDto(
    int Id,
    string Name,
    string ShortName,
    string LogoUrl,
    string Abbreviation,
    int? ApiTeamId
);
