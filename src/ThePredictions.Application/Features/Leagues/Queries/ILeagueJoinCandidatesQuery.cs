namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Everybody who holds a Season Pass for a league's season and has no membership of that league yet. Returns
/// <c>null</c> when the league does not exist.
/// </summary>
/// <remarks>
/// Null rather than an empty list, because a caller cannot tell those apart and they mean different things: a league
/// whose whole season has already joined it is a perfectly good state, and a league id that matches nothing is a 404.
///
/// "No membership yet" means no row at all, of any status - not merely "not approved". Pending and rejected members
/// already have a row, and the Approved and Rejected tabs on the same page are where those are dealt with. Offering
/// them here would produce a duplicate the domain refuses anyway.
/// </remarks>
public interface ILeagueJoinCandidatesQuery
{
    Task<IReadOnlyList<LeagueJoinCandidateRow>?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
