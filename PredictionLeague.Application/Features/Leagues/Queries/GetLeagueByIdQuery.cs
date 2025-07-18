using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueByIdQuery : IRequest<LeagueDto?>
{
    public int Id { get; }
    
    public GetLeagueByIdQuery(int leagueId)
    {
        Id = leagueId;
    }
}