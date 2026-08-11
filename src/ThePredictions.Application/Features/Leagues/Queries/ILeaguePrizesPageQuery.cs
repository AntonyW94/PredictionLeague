namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads a league's prize page: the league and its season, and the prizes as configured. Returns <c>null</c> when the
/// league does not exist.
/// </summary>
public interface ILeaguePrizesPageQuery
{
    Task<LeaguePrizesPageData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
