using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Seasons;

[ExcludeFromCodeCoverage]
public record SeasonLookupDto(
    int Id,
    string Name,
    DateTime StartDateUtc,
    bool IsTournament = false);
