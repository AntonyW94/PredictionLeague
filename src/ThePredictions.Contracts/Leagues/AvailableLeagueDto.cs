using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public record AvailableLeagueDto(
    int Id,
    string Name,
    string SeasonName,
    decimal Price,
    DateTime EntryDeadlineUtc,
    int MemberCount,
    decimal EstPot,
    bool IsPrivate = false);
