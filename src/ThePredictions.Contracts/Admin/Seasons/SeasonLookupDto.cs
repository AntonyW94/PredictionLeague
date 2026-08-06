using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Seasons;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record SeasonLookupDto(
    int Id,
    string Name,
    DateTime StartDateUtc,
    bool IsTournament = false);
