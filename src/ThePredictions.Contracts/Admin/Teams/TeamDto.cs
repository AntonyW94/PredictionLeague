using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Teams;

/// <summary>
/// One team. <see cref="LogoUrl"/> is nullable because the column is - a team added by hand before its badge has been
/// found has none, and two of the three reads of this table used to claim otherwise.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record TeamDto(
    int Id,
    string Name,
    string ShortName,
    string? LogoUrl,
    string Abbreviation,
    int? ApiTeamId
);
