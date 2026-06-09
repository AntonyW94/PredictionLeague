using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IPrizeNotificationRepository
{
    Task AddNotificationsAsync(IEnumerable<PrizeNotification> notifications, CancellationToken cancellationToken);
}
