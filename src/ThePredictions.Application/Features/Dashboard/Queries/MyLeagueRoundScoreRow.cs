using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// What one member scored in one round of one of the player's leagues.
/// </summary>
/// <remarks>
/// Not <see cref="DashboardLeagueMemberPointsRow"/>, which carries no round because the leaderboards tile only
/// totals: here the rounds and the calendar months they fall in are the periods being won, so each row has to say
/// which round it came from.
///
/// Every member's scores, not just the player's - a round cannot be known to be won without the scores it was won
/// against.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MyLeagueRoundScoreRow(int LeagueId, string UserId, int RoundId, int BoostedPoints);
