namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>A user's final cumulative rank within a league at season end.</summary>
public record UserLeagueRank(string UserId, int LeagueId, int Rank);
