using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Users;

/// <summary>One league an account has asked to join, and what that cost.</summary>
/// <remarks>
/// <see cref="Price"/> is the league's entry fee, which is only money this account spent when the membership was
/// approved and the league is a paid one - the rule lives in the handler, and the screen shows a pending request with
/// its fee struck through.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record UserLeagueMembershipDto(
    int LeagueId,
    string LeagueName,
    int SeasonId,
    string SeasonName,
    bool IsCurrentSeason,
    LeagueMemberStatus Status,
    bool IsFree,
    decimal Price);
