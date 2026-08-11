namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads what is needed to show a league's payment details, and the two facts that decide whether the caller may see
/// them. Returns <c>null</c> when the league does not exist.
/// </summary>
/// <remarks>
/// The bank details come back encrypted. Decrypting them is the handler's job and happens only after the authorisation
/// rule has passed, so an adapter cannot hand a caller readable bank details before anyone has checked who they are.
/// </remarks>
public interface ILeaguePaymentInfoQuery
{
    Task<LeaguePaymentInfoRow?> ExecuteAsync(int leagueId, string requestingUserId, CancellationToken cancellationToken);
}
