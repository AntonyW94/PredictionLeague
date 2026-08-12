using MediatR;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The player's own outstanding league requests, newest first.
/// </summary>
public class GetPendingRequestsQueryHandler(IMyLeagueRequestsQuery requestsQuery)
    : IRequestHandler<GetPendingRequestsQuery, IEnumerable<LeagueRequestDto>>
{

    public async Task<IEnumerable<LeagueRequestDto>> Handle(
        GetPendingRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var requests = await requestsQuery.ExecuteAsync(request.UserId, cancellationToken);

        return requests
            .Where(IsWorthShowing)
            .OrderByDescending(row => row.JoinedAtUtc)
            .Select(row => new LeagueRequestDto(
                row.LeagueId,
                row.LeagueName,
                row.SeasonName,
                row.Status,
                row.JoinedAtUtc,
                row.EntryDeadlineUtc,
                PlayerDisplayName.Format(row.AdminFirstName, row.AdminLastName),
                row.MemberCount,
                row.Price,
                PrizeFund.Total(row.Price, row.MemberCount, row.PrizeFundOverride)))
            .ToList();
    }

    /// <summary>
    /// Whether a request still belongs on the player's dashboard: one they are waiting on, or one that was turned down and
    /// whose notice they have not yet dismissed.
    /// </summary>
    /// <remarks>
    /// The dismissal is what stops a rejection following somebody around for ever. It only applies to rejections - a pending
    /// request cannot be dismissed, because the player is still waiting for an answer to it.
    /// </remarks>
    private static bool IsWorthShowing(MyLeagueRequestRow request)
    {
        if (request.Status == LeagueMemberStatus.Pending)
            return true;

        if (request.Status != LeagueMemberStatus.Rejected)
            return false;

        return !request.IsAlertDismissed;
    }
}
