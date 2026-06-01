using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IServiceFeeRepository
{
    Task<ServiceFee?> GetByProviderAsync(ServiceFeeProvider provider, CancellationToken cancellationToken);
    Task AddAsync(ServiceFee serviceFee, CancellationToken cancellationToken);
    Task UpdateAsync(ServiceFee serviceFee, CancellationToken cancellationToken);
}
