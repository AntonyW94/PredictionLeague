namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>A round window restricting when (and how often) a league boost can be used.</summary>
public record LeagueWelcomeBoostWindow(
    int StartRoundNumber,
    int EndRoundNumber,
    int MaxUsesInWindow);
