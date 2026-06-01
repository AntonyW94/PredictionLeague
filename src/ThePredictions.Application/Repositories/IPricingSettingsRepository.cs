using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IPricingSettingsRepository
{
    Task<PricingSettings?> GetAsync(CancellationToken cancellationToken);
    Task AddAsync(PricingSettings settings, CancellationToken cancellationToken);
    Task UpdateAsync(PricingSettings settings, CancellationToken cancellationToken);
}
