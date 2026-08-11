using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// An approved member of the league, with the pre-round position cached for them.
///
/// Name parts rather than a formatted name, because formatting and ordering are both C# rules now - and the
/// two want different forms: the screen shows "Ada L" while joint positions are ordered by the full name.
/// </summary>
/// <remarks>
/// <see cref="SnapshotOverallRank"/> is read, never recomputed. It is the cached value maintained on the write
/// path by <c>LeagueStatsRepository</c> under ADR-0015, which exists because computing these ranks live cost
/// roughly 400ms of query planning per request. Whether it is <i>shown</i> is a rule and lives in the handler;
/// what it contains is not this work's business.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeaderboardMemberRow(
    string UserId,
    string FirstName,
    string LastName,
    int? SnapshotOverallRank);
