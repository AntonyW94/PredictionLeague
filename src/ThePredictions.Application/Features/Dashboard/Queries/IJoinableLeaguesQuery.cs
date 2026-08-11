namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Reads every league the player is not already involved with, and the facts needed to decide whether they could join it.
/// </summary>
/// <remarks>
/// "Not involved with" means no membership row of any status - approved, pending, or rejected. Somebody who was turned
/// away is not offered the league a second time, which was the old <c>NOT EXISTS</c> with no status filter.
///
/// Serves both discovery queries: the list of leagues on offer, and the hint that says private leagues are available. They
/// filter this differently, and how they differ is recorded in the plan document.
/// </remarks>
public interface IJoinableLeaguesQuery
{
    Task<IReadOnlyList<JoinableLeagueRow>> ExecuteAsync(string userId, CancellationToken cancellationToken);
}
