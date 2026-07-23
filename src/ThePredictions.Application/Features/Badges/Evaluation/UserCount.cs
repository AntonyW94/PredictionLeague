namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>A per-user integer metric (cumulative exact scores, streak length, etc.).</summary>
public record UserCount(string UserId, int Count);
