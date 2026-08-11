using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// An approved member of one of the player's leagues, with that league's cached pre-round position for them.
/// </summary>
/// <remarks>
/// Not <c>LeaderboardMemberRow</c>, which is otherwise the same shape: that one belongs to a single league's
/// table and so needs no league id. Adding one there for this caller's sake would leave every other leaderboard
/// carrying a field it already knows the answer to.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record DashboardLeagueMemberRow(
    int LeagueId,
    string UserId,
    string FirstName,
    string LastName,
    int? SnapshotRank);
