using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetMonthlyLeaderboardQuery : IRequest<IEnumerable<LeaderboardEntryDto>>
{
    public int LeagueId { get; }
    public int Month { get; }

    public GetMonthlyLeaderboardQuery(int leagueId, int month)
    {
        LeagueId = leagueId;
        Month = month;
    }
}