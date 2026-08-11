namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads a league's winnings page: the league and its season, the prizes on offer, the prizes won, and the members who
/// could win them. Returns <c>null</c> when the league does not exist.
/// </summary>
public interface IWinningsQuery
{
    Task<WinningsData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
