namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads a league's stored bank details, still encrypted, along with who administers it - or nothing if there is no such league.
/// </summary>
/// <remarks>
/// The administrator's id comes back so the handler can check who is asking <b>before</b> anything is decrypted. That ordering
/// is the whole security of this read.
/// </remarks>
public interface ILeagueBankDetailsQuery
{
    Task<EncryptedLeagueBankDetailsRow?> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
