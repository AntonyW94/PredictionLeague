using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The months a league's leaderboard can be filtered by, in the order the season runs them.
/// </summary>
/// <remarks>
/// The sibling of <see cref="GetStagesForLeagueQueryHandler"/>: same rows, same progress counts, different grouping.
/// One read now serves both.
/// </remarks>
public class GetMonthsForLeagueQueryHandler(
    ILeagueSeasonRoundsQuery seasonRoundsQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetMonthsForLeagueQuery, IEnumerable<MonthDto>>
{
    public async Task<IEnumerable<MonthDto>> Handle(GetMonthsForLeagueQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var rounds = await seasonRoundsQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (rounds.Count == 0)
            return [];

        var seasonStartMonth = rounds.Min(round => round.StartDateUtc).Month;

        var months = rounds
            .GroupBy(round => round.StartDateUtc.Month)
            .Select(month => new
            {
                Month = month.Key,
                Progress = RoundProgress.Of(month.Select(round => round.Status))
            })
            .Where(month => month.Progress.HasVisibleRound)
            .ToList();

        return SeasonMonthOrder.Apply(months, month => month.Month, seasonStartMonth)
            .Select(month => new MonthDto(
                month.Month,
                MonthName.Of(month.Month)!,
                month.Progress.RoundsRemaining,
                month.Progress.RoundsCompleted))
            .ToList();
    }
}
