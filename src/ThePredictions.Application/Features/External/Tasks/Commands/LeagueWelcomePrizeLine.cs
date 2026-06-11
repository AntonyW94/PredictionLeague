namespace ThePredictions.Application.Features.External.Tasks.Commands;

/// <summary>One prize row in the welcome email; the top prize of a ranked category gets the trophy.</summary>
public record LeagueWelcomePrizeLine(string Title, string Value, bool IsTop);
