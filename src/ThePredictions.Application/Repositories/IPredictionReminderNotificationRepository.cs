using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IPredictionReminderNotificationRepository
{
    /// <summary>
    /// Returns the most recent reminder timestamp for each of the given users in the round, so the
    /// send throttle can skip anyone reminded within the throttle window. Users never reminded are
    /// simply absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<string, DateTime>> GetLastRemindedUtcAsync(int roundId, IEnumerable<string> userIds, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a reminder log row, or refreshes <see cref="PredictionReminderNotification.LastRemindedUtc"/>
    /// (and the triggering user) if one already exists for the (round, user) pair.
    /// </summary>
    Task UpsertAsync(PredictionReminderNotification notification, CancellationToken cancellationToken);
}
