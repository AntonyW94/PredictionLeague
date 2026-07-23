namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>
/// A socialite tier a user qualifies for, with the date they joined their Nth league so the award can
/// be dated accurately (Rank is the ordinal join position: 1, 3 or 5).
/// </summary>
public record SocialiteAward(string UserId, int Rank, DateTime AwardedUtc);
