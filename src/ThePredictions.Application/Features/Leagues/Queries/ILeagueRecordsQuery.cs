namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads the raw material behind a league's records tile. Returns <c>null</c> when the league does not exist.
/// </summary>
public interface ILeagueRecordsQuery
{
    Task<LeagueRecordsData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
