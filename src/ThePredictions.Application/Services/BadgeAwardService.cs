using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

public class BadgeAwardService(IUserBadgeRepository userBadgeRepository, IDateTimeProvider dateTimeProvider) : IBadgeAwardService
{
    // Dated now (the user earned it just now). AwardAsync is a no-op if they already hold it, so this
    // is safe to call unconditionally after the qualifying action.
    public Task AwardAsync(string userId, string badgeKey, CancellationToken cancellationToken)
        => userBadgeRepository.AwardAsync(AwardedBadge.Create(userId, badgeKey, dateTimeProvider.UtcNow), cancellationToken);
}
