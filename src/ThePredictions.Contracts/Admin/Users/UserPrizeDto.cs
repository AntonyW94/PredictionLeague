using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Users;

/// <summary>One prize an account has won.</summary>
/// <remarks>
/// <see cref="Title"/> is composed on the server rather than here, because naming a prize is a rule with several cases -
/// a named round, a month, a tournament stage - and the pieces it is composed from (the prize type, the stage, the round
/// number, the month) are not worth carrying to the client only to be reassembled there.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record UserPrizeDto(
    int LeagueId,
    string LeagueName,
    int SeasonId,
    string SeasonName,
    bool IsCurrentSeason,
    string Title,
    decimal Amount,
    DateTime AwardedDateUtc);
