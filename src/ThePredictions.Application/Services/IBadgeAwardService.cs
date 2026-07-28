namespace ThePredictions.Application.Services;

/// <summary>
/// Awards a badge the moment a user earns it through an action (e.g. adding a mobile number),
/// rather than waiting for the next round-completion evaluation. Idempotent - the round evaluator
/// re-checks the same badges as a safety net, so a missed on-action award is still caught later.
/// </summary>
public interface IBadgeAwardService
{
    Task AwardAsync(string userId, string badgeKey, CancellationToken cancellationToken);
}
