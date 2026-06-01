using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IEmailConfirmationTokenRepository
{
    Task CreateAsync(EmailConfirmationToken token, CancellationToken cancellationToken = default);
    Task<EmailConfirmationToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
