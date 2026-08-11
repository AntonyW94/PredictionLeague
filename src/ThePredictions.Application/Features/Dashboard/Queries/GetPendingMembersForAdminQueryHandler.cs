using MediatR;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// What an administrator has waiting for them: their leagues that are still taking entries, and the requests to join them.
/// </summary>
public class GetPendingMembersForAdminQueryHandler(
    IAdminPendingMembersQuery pendingMembersQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetPendingMembersForAdminQuery, PendingMembersResultDto>
{
    public async Task<PendingMembersResultDto> Handle(
        GetPendingMembersForAdminQuery request,
        CancellationToken cancellationToken)
    {
        var data = await pendingMembersQuery.ExecuteAsync(request.UserId, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        var openLeagues = data.Leagues
            .Where(league => IsStillTakingEntries(league, utcNow))
            .OrderBy(league => league.LeagueName, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        if (openLeagues.Count == 0)
            return new PendingMembersResultDto { IsAdminOfOpenLeague = false };

        var openLeagueIds = openLeagues.Select(league => league.LeagueId).ToHashSet();

        return new PendingMembersResultDto
        {
            IsAdminOfOpenLeague = true,
            // The deadline is safe to read as non-null here: a league without one is not still taking entries, so
            // IsStillTakingEntries has already excluded it. No sentinel is needed, unlike on the pages that show a league
            // whatever its deadline.
            AdminLeagues = openLeagues
                .Select(league => new AdminLeagueSummaryDto(
                    league.LeagueId,
                    league.LeagueName,
                    league.EntryDeadlineUtc!.Value,
                    league.MemberCount,
                    league.PendingCount,
                    league.Price,
                    league.IsFree,
                    league.EntryCode))
                .ToList(),
            Members = data.PendingMembers
                .Where(member => openLeagueIds.Contains(member.LeagueId))
                .OrderBy(member => member.LeagueName, StringComparer.InvariantCultureIgnoreCase)
                .ThenBy(member => member.JoinedAtUtc)
                .Select(member => new PendingLeagueMemberDto(
                    member.LeagueId,
                    member.LeagueName,
                    member.UserId,
                    PlayerDisplayName.Format(member.FirstName, member.LastName),
                    member.JoinedAtUtc))
                .ToList()
        };
    }

    /// <summary>
    /// Whether a league is still taking entries, and so still worth an administrator's attention.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> <c>LeagueEntry.IsOpen</c>, which the league-discovery queries use. That one is strictly
    /// greater than: a league whose deadline is exactly now is no longer joinable. This one was written
    /// <c>EntryDeadlineUtc &gt;= GETUTCDATE()</c>, so at that same instant the administrator still sees it.
    ///
    /// One tick apart, and almost certainly incidental rather than intended - preserved rather than aligned, and recorded in
    /// the plan document. A league with no deadline at all is excluded either way, which the old <c>&gt;=</c> did through
    /// SQL's treatment of nulls and this does by saying so.
    /// </remarks>
    private static bool IsStillTakingEntries(AdministeredLeagueRow league, DateTime utcNow)
    {
        if (league.EntryDeadlineUtc is not { } deadline)
            return false;

        return deadline >= utcNow;
    }
}
