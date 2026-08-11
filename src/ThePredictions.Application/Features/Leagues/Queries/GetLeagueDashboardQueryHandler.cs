using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's dashboard: its header, its rounds, and its members.
/// </summary>
/// <remarks>
/// The one query that answers "no such league" rather than "not allowed", and it matters: a stranger who is told
/// they are forbidden has learned that the league exists, so both cases return the same 404. That rule was already
/// in C# but its membership check was a fourth copy of the same <c>COUNT(*)</c> - it now goes through
/// <see cref="ILeagueMembershipService"/> like every other league query, while keeping its own answer to a failure.
/// </remarks>
public class GetLeagueDashboardQueryHandler(
    ILeagueDashboardQuery dashboardQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeagueDashboardQuery, LeagueDashboardDto>
{
    public async Task<LeagueDashboardDto> Handle(GetLeagueDashboardQuery request, CancellationToken cancellationToken)
    {
        await EnsureVisibleAsync(request, cancellationToken);

        var data = await dashboardQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (data is null)
            throw new EntityNotFoundException("League", request.LeagueId);

        var header = data.Header;

        return new LeagueDashboardDto
        {
            LeagueName = header.Name,
            CompetitionType = header.CompetitionType,
            SeasonStartDateUtc = header.SeasonStartDateUtc,
            EntryDeadlineUtc = header.EntryDeadlineUtc,
            MemberCount = header.MemberCount,
            TotalPrizeFund = PrizeFund.Total(header.Price, header.MemberCount, header.PrizeFundOverride),
            IsFinished = SeasonCompletion.IsFinished(header.CompletedRoundCount, header.NumberOfRounds),
            IsFree = header.IsFree,
            Members = MembersOn(data.Members),
            ViewableRounds = RoundsOn(data.Rounds)
        };
    }

    /// <summary>
    /// Whether the caller may see this league at all.
    /// </summary>
    /// <remarks>
    /// A site administrator always may. Anyone else must be an approved member, and if they are not the answer is
    /// deliberately "no such league" rather than "not allowed" - the two cases are indistinguishable from outside, so
    /// a stranger cannot map out which leagues exist by reading status codes. This is not a mistaken use of
    /// <c>EntityNotFoundException</c>: the league may well exist.
    /// </remarks>
    private async Task EnsureVisibleAsync(GetLeagueDashboardQuery request, CancellationToken cancellationToken)
    {
        if (request.IsAdmin)
            return;

        var isMember = await membershipService.IsApprovedMemberAsync(request.LeagueId, request.UserId, cancellationToken);

        if (!isMember)
            throw new EntityNotFoundException("League", request.LeagueId);
    }

    /// <summary>
    /// The members shown, newest joiners last and alphabetical by name.
    /// </summary>
    /// <remarks>
    /// Approved members and pending requests both appear, because the administrator approves people from here. A
    /// rejected request does not - that person was turned away and listing them would invite the question a second
    /// time.
    /// </remarks>
    private static List<LeagueDashboardMemberDto> MembersOn(IReadOnlyList<LeagueDashboardMemberRow> members) =>
        members
            .Where(member => member.Status is LeagueMemberStatus.Approved or LeagueMemberStatus.Pending)
            .OrderBy(member => member.FirstName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(member => member.LastName, StringComparer.InvariantCultureIgnoreCase)
            .Select(member => new LeagueDashboardMemberDto(
                PlayerDisplayName.Format(member.FirstName, member.LastName),
                member.Status.ToString(),
                member.JoinedAtUtc))
            .ToList();

    /// <summary>The season's rounds, newest first, which is the order the dashboard reads them in.</summary>
    private static List<RoundDto> RoundsOn(IReadOnlyList<LeagueDashboardRoundRow> rounds) =>
        rounds
            .OrderByDescending(round => round.RoundNumber)
            .Select(round => new RoundDto(
                round.RoundId,
                round.SeasonId,
                round.RoundNumber,
                round.ApiRoundName,
                round.StartDateUtc,
                round.DeadlineUtc,
                round.Status,
                round.MatchCount))
            .ToList();
}
