using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>The season's name, how many holders match the filters, and what they have paid between them.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPassHoldersSummary(string SeasonName, int MatchingCount, decimal TotalCollected);
