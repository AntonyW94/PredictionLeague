using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Seasons;

/// <summary>
/// A suggested Standard Season Pass price for a season draft, with an explainable breakdown.
/// Advisory only - the admin can override it. When there is no comparable prior season,
/// <see cref="SuggestedStandardPrice"/> is null and <see cref="UnavailableReason"/> explains why.
/// </summary>
[ExcludeFromCodeCoverage]
public record PriceRecommendationDto(
    decimal? SuggestedStandardPrice,
    string? UnavailableReason,
    decimal AnnualRunningCost,
    int SeasonRounds,
    int TotalPaidRoundsInHorizon,
    decimal Weight,
    decimal ApportionedCost,
    decimal BufferRate,
    decimal TargetWithBuffer,
    int? ExpectedPlayers,
    decimal? PerPlayer,
    decimal? FeeGrossedUp,
    bool FloorApplied);
