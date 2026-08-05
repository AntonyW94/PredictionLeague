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
/// <param name="AcquiredFromUtc">Only passes acquired on or after this date. Time of day is ignored.</param>
/// <param name="AcquiredToUtc">Only passes acquired on or before this date. The whole day is included.</param>
/// <param name="MinimumPaid">Only passes whose total paid (including any SMS fee) is at least this.</param>
/// <param name="MaximumPaid">Only passes whose total paid (including any SMS fee) is at most this.</param>
public record GetSeasonPassHoldersQuery(
    int SeasonId,
    int Page,
    int PageSize,
    SeasonPassHolderSortField SortField,
    SortDirection SortDirection,
    string? NameFilter,
    DateTime? AcquiredFromUtc,
    DateTime? AcquiredToUtc,
    decimal? MinimumPaid,
    decimal? MaximumPaid
) : IRequest<SeasonPassHoldersPageDto?>;
