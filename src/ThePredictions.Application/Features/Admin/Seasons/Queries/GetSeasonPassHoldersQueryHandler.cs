using MediatR;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>One page of a season's pass holders, with the totals for everybody the filters match.</summary>
public class GetSeasonPassHoldersQueryHandler(ISeasonPassHoldersQuery seasonPassHoldersQuery)
    : IRequestHandler<GetSeasonPassHoldersQuery, SeasonPassHoldersPageDto>
{
    public async Task<SeasonPassHoldersPageDto> Handle(GetSeasonPassHoldersQuery request, CancellationToken cancellationToken)
    {
        var pageSize = PageSizes.Clamp(request.PageSize);
        var criteria = new SeasonPassHoldersCriteria(
            request.SeasonId,
            request.NameFilter,
            request.AcquiredFromUtc,
            request.AcquiredBeforeUtc,
            request.MinimumPaid,
            request.MaximumPaid);

        var summary = await seasonPassHoldersQuery.GetSummaryAsync(criteria, cancellationToken)
                      ?? throw new EntityNotFoundException("Season", request.SeasonId);

        if (summary.MatchingCount == 0)
            return new SeasonPassHoldersPageDto(summary.SeasonName, 0, PagedResult<SeasonPassHolderDto>.Empty(pageSize));

        var page = ClampPage(request.Page, summary.MatchingCount, pageSize);

        var holders = await seasonPassHoldersQuery.GetPageAsync(
            criteria,
            new SeasonPassHoldersPaging(request.SortField, request.SortDirection, (page - 1) * pageSize, pageSize),
            cancellationToken);

        var items = holders.Select(ToDto).ToList();

        return new SeasonPassHoldersPageDto(
            summary.SeasonName,
            summary.TotalCollected,
            new PagedResult<SeasonPassHolderDto>(items, page, pageSize, summary.MatchingCount));
    }

    /// <summary>
    /// Keeps a stale or hand-typed page number in range, so tightening a filter lands on the last page of the smaller
    /// result set rather than on an empty one.
    /// </summary>
    private static int ClampPage(int requestedPage, int matchingCount, int pageSize)
    {
        var lastPage = (int)Math.Ceiling(matchingCount / (double)pageSize);

        return Math.Clamp(requestedPage, 1, lastPage);
    }

    private static SeasonPassHolderDto ToDto(SeasonPassHolderRow holder) =>
        new(holder.UserId,
            holder.FullName,
            holder.Email,
            holder.Tier,
            holder.Source,
            holder.AmountPaid,
            holder.SmsFeePaid,
            holder.CreatedAtUtc);
}
