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
    /// <summary>
    /// Shown for a request whose league has no entry deadline set.
    /// </summary>
    /// <remarks>
    /// The same sentinel the league settings and prize pages use, and for the same reason: the contract's property is not
    /// nullable. What is different here is that this read has no deadline filter, so a league without one would previously
    /// have failed to materialise rather than merely showing an odd date - see the plan document.
    /// </remarks>
    private static readonly DateTime NoEntryDeadline = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
                row.EntryDeadlineUtc ?? NoEntryDeadline,
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
