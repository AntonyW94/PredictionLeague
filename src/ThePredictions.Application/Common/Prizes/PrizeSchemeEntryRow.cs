using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>One category of a league's prize scheme, and what it pays per entry.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeSchemeEntryRow(PrizeType Category, int PerEntryPounds, string? RankTableJson);
