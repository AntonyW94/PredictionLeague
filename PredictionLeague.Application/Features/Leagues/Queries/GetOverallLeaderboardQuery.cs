using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetOverallLeaderboardQuery : IRequest<IEnumerable<LeaderboardEntryDto>>
{
    public int LeagueId { get; }

    public GetOverallLeaderboardQuery(int leagueId)
    {
        LeagueId = leagueId;
    }
}