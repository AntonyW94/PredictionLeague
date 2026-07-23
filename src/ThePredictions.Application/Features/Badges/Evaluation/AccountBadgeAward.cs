namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>
/// An account/setup badge a user qualifies for (add a mobile number, add bank details, create a league),
/// with the date to award it (the relevant record's creation time where known).
/// </summary>
public record AccountBadgeAward(string UserId, string BadgeKey, DateTime AwardedUtc);
