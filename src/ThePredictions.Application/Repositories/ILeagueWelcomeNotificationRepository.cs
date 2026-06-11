using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface ILeagueWelcomeNotificationRepository
{
    Task AddNotificationsAsync(IEnumerable<LeagueWelcomeNotification> notifications, CancellationToken cancellationToken);
}
