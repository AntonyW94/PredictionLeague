namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// Reads everything behind the league's boost-usage table.
///
/// Returns <c>null</c> when the league does not exist, which the handler treats as an empty page. The reply
/// is deliberately unshaped and uncensored: the adapter's job ends at "these are the facts", and every rule
/// about what they mean - visibility, points gained, window status, display names, ordering - is applied in
/// C# where it can be unit tested at a pinned instant.
/// </summary>
public interface ILeagueBoostUsageQuery
{
    Task<LeagueBoostUsageData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
