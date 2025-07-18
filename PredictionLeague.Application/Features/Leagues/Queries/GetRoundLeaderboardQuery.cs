using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetRoundLeaderboardQuery : IRequest<IEnumerable<LeaderboardEntryDto>>
{
    public int LeagueId { get; }
    public int RoundId { get; }

    public GetRoundLeaderboardQuery(int leagueId, int roundId)
    {
        LeagueId = leagueId;
        RoundId = roundId;
    }
}