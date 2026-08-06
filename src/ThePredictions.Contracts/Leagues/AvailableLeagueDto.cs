using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record AvailableLeagueDto(
    int Id,
    string Name,
    string SeasonName,
    decimal Price,
    DateTime EntryDeadlineUtc,
    int MemberCount,
    decimal EstPot,
    bool IsPrivate = false);
