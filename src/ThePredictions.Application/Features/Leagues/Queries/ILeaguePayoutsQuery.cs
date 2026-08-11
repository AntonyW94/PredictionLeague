namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads a league's payout screen: who has won what, what has already been paid, and the winners' bank details.
/// Returns <c>null</c> when the league does not exist.
/// </summary>
/// <remarks>
/// The bank details come back encrypted, and only for the league's winners - not for every player who has ever shared
/// them. Decrypting happens in the handler, after the administrator check.
/// </remarks>
public interface ILeaguePayoutsQuery
{
    Task<LeaguePayoutsData?> ExecuteAsync(int leagueId, string requestingUserId, CancellationToken cancellationToken);
}
