using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>Which page of holders to return, and in what order.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPassHoldersPaging(
    SeasonPassHolderSortField SortField,
    SortDirection SortDirection,
    int Skip,
    int Take);
