using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>
/// One page of the Season Pass holders for a season, sorted and filtered on the server so a
/// season with thousands of holders never has to be sent to the browser in one go.
/// </summary>
/// <param name="SeasonId">The season whose holders are being listed.</param>
/// <param name="Page">1-based page number. Clamped into range against the matching count.</param>
/// <param name="PageSize">Snapped to one of <see cref="PageSizes.Allowed"/>.</param>
/// <param name="SortField">Column to order by.</param>
/// <param name="SortDirection">Direction to order in.</param>
/// <param name="NameFilter">Matches holders whose full name contains this text. Ignored when blank.</param>
/// <param name="AcquiredFromUtc">
/// Only passes acquired at or after this instant. An exact instant, not a date: whoever is asking
/// decides where their day starts, because only they know what time zone they are in.
/// </param>
/// <param name="AcquiredBeforeUtc">
/// Only passes acquired strictly before this instant. Exclusive, so to cover a whole day pass the
/// instant the following day begins.
/// </param>
/// <param name="MinimumPaid">Only passes whose total paid (including any SMS fee) is at least this.</param>
/// <param name="MaximumPaid">Only passes whose total paid (including any SMS fee) is at most this.</param>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetSeasonPassHoldersQuery(
    int SeasonId,
    int Page,
    int PageSize,
    SeasonPassHolderSortField SortField,
    SortDirection SortDirection,
    string? NameFilter,
    DateTime? AcquiredFromUtc,
    DateTime? AcquiredBeforeUtc,
    decimal? MinimumPaid,
    decimal? MaximumPaid
) : IRequest<SeasonPassHoldersPageDto?>;
