namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>A boost enabled for the league, with its season cap and any round-window restrictions.</summary>
public record LeagueWelcomeBoost(
    string Name,
    string? Description,
    string? ImageUrl,
    int TotalUsesPerSeason,
    IReadOnlyList<LeagueWelcomeBoostWindow> Windows);
