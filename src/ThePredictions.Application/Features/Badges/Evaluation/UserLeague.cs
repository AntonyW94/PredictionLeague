namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>A user paired with the league the badge was earned in (provenance).</summary>
public record UserLeague(string UserId, int LeagueId);
