namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>A user's aggregated result for one round (from RoundResults).</summary>
public record RoundUserResult(string UserId, int ExactScoreCount, int TotalPoints);
