using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class FetchLeagueMembersQuery : IRequest<LeagueMembersPageDto?>
{
    public int LeagueId { get; }

    public FetchLeagueMembersQuery(int leagueId)
    {
        LeagueId = leagueId;
    }
}