namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads one league's settings. Returns <c>null</c> when it does not exist.
/// </summary>
public interface ILeagueDetailQuery
{
    Task<LeagueDetailRow?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
