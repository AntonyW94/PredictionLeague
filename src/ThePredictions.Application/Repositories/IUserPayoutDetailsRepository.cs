using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IUserPayoutDetailsRepository
{
    Task<UserPayoutDetails?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task UpsertAsync(UserPayoutDetails payoutDetails, CancellationToken cancellationToken);
    Task DeleteAsync(string userId, CancellationToken cancellationToken);
}
