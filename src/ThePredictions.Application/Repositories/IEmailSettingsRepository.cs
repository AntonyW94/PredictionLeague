using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IEmailSettingsRepository
{
    Task<EmailSettings?> GetAsync(CancellationToken cancellationToken);
    Task AddAsync(EmailSettings settings, CancellationToken cancellationToken);
    Task UpdateAsync(EmailSettings settings, CancellationToken cancellationToken);
}
