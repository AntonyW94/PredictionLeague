namespace ThePredictions.Application.Features.Account.Queries;

/// <summary>
/// Reads a player's stored bank details, still encrypted, and the administrators who would be paying them.
/// </summary>
/// <remarks>
/// The values are handed over as stored. Decrypting them is the handler's job, and whether a partly-filled set counts as
/// "having details" is a rule that has to be applied after decryption rather than to the ciphertext.
/// </remarks>
public interface IMyPayoutDetailsQuery
{
    Task<EncryptedPayoutDetailsRow?> GetDetailsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The administrators of the prize-paying leagues this player belongs to, excluding themselves - the people who will be
    /// sending them money, so the screen can say who to expect it from.
    /// </summary>
    Task<IReadOnlyList<PayingAdministratorRow>> GetPayingAdministratorsAsync(string userId, CancellationToken cancellationToken);
}
