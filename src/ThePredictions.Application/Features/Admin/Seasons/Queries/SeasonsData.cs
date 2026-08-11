using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>What <see cref="ISeasonsQuery"/> returns.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonsData(
    IReadOnlyList<AdminSeasonRow> Seasons,
    IReadOnlyList<SeasonRoundStatusRow> Rounds,
    IReadOnlyList<SeasonFixtureTeamsRow> Fixtures);
