using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>One season a pass could be held for, with its competition and its prices.</summary>
/// <remarks>
/// Whether the season needs paying for is decided from <see cref="StandardPrice"/> - a season with no price is a free one
/// - and that was a <c>CASE WHEN ... IS NOT NULL</c> in three statements.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPassSeasonRow(
    int Id,
    string Name,
    DateTime StartDateUtc,
    bool IsActive,
    string? CompetitionLogoUrl,
    string? CompetitionDescription,
    decimal? StandardPrice,
    decimal? PremiumPrice);
