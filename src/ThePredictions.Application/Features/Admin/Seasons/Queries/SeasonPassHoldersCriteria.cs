using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>Which of a season's pass holders to look at.</summary>
/// <remarks>
/// <see cref="NameFilter"/> is what the administrator typed, untouched. Escaping it is the adapter's job, because what
/// needs escaping depends on how the adapter searches.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPassHoldersCriteria(
    int SeasonId,
    string? NameFilter,
    DateTime? AcquiredFromUtc,
    DateTime? AcquiredBeforeUtc,
    decimal? MinimumPaid,
    decimal? MaximumPaid);
