using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>What one round of the season is called, so a round prize can name the round it was won in.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonRoundNameRow(int RoundNumber, string DisplayName);
