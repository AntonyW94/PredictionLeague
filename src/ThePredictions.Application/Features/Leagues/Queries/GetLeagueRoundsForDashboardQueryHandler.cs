using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The rounds a member can pick from on a league's dashboard, newest first.
/// </summary>
public class GetLeagueRoundsForDashboardQueryHandler(
    ILeagueRoundsQuery roundsQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeagueRoundsForDashboardQuery, IEnumerable<RoundDto>>
{
    public async Task<IEnumerable<RoundDto>> Handle(
        GetLeagueRoundsForDashboardQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var rounds = await roundsQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        return rounds
            .Where(IsPickable)
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

    /// <summary>
    /// Whether a member can pick this round: published, or finished.
    /// </summary>
    /// <remarks>
    /// A draft is not yet something players can see, which is consistent with the rest of the site. A round
    /// <b>in progress</b> is excluded too, which is less obviously right - it is the round most likely to be worth
    /// looking at - and this preserves the old <c>r.[Status] IN (@PublishedStatus, @CompletedStatus)</c> exactly rather
    /// than quietly widening it. Recorded in the plan document as a question for the owner.
    ///
    /// Deliberately not shared with the league dashboard, which lists every round including drafts and fills the same
    /// <c>ViewableRounds</c> field from the same rows. Two callers, one read, two different answers to "viewable".
    /// </remarks>
    private static bool IsPickable(LeagueRoundRow round) =>
        round.Status is RoundStatus.Published or RoundStatus.Completed;
}
