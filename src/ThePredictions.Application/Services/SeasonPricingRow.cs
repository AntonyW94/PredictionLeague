using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Services;

/// <summary>One season, as the price recommendation judges it.</summary>
/// <remarks>
/// <see cref="StandardPrice"/> is here only so the calculation can tell a paid season from a free one - a free season
/// contributes nothing towards covering the annual costs, so it does not share them.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPricingRow(
    int Id,
    int CompetitionId,
    int NumberOfRounds,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    decimal? StandardPrice);
