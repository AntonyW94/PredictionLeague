namespace ThePredictions.Application.Features.Badges;

/// <summary>
/// A badge genuinely awarded to a user during a round evaluation (a real new insert, not an
/// idempotent no-op). Surfaced by <c>EvaluateBadgesForRoundCommand</c> and threaded into the
/// round-results digest so the email can celebrate what the player just earned.
/// </summary>
public record RoundBadgeAward(string UserId, string BadgeKey);
