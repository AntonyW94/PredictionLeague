namespace ThePredictions.Application.Features.External.Tasks.Commands;

/// <summary>One category of the welcome email's prize breakdown (Overall, a stage, or Other prizes).</summary>
public record LeagueWelcomePrizeSection(string Title, IReadOnlyList<LeagueWelcomePrizeLine> Prizes);
