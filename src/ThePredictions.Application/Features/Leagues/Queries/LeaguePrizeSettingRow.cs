using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One prize a league pays out: what it is for, which position, how much, and for a stage prize which stage.
/// </summary>
/// <remarks>
/// Nothing here is nullable except the stage, which only a stage prize has. In the old flattened result all four were,
/// because a league with no prizes had to be expressible as a row of nulls.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeaguePrizeSettingRow(PrizeType PrizeType, int Rank, decimal PrizeAmount, string? Stage);
