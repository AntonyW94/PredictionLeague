namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>
/// A user's aggregated result for one round (from RoundResults). Uses the exact/correct counts rather than
/// TotalPoints, which is only partially populated - a correct or exact result means they scored points.
/// </summary>
public record RoundUserResult(string UserId, int ExactScoreCount, int CorrectResultCount);
