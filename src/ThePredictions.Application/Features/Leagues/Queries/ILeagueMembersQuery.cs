namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads a league's name and everyone who has ever asked to be in it. Returns <c>null</c> when the league does not
/// exist.
/// </summary>
/// <remarks>
/// Name and members together, so that a league with no members still has a name to show. The old handler got there by
/// reading the members, noticing the list was empty, and then running a second statement for the name - which meant the
/// "does this league exist" answer depended on whether anybody had joined it.
/// </remarks>
public interface ILeagueMembersQuery
{
    Task<LeagueMembersData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
