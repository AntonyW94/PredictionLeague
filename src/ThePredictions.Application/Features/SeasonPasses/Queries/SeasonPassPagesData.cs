using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>What <see cref="ISeasonPassPagesQuery"/> returns.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPassPagesData(
    IReadOnlyList<SeasonPassSeasonRow> Seasons,
    IReadOnlyList<SeasonLeagueEntryRow> Leagues,
    IReadOnlyList<SeasonPassHolderCountRow> HolderCounts,
    IReadOnlyList<HeldSeasonPassRow> HeldPasses);
