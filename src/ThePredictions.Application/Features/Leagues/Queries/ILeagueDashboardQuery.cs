namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads a league's dashboard: the league and its season, its rounds, and the people in it.
///
/// Returns <c>null</c> when the league does not exist.
/// </summary>
public interface ILeagueDashboardQuery
{
    Task<LeagueDashboardData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
