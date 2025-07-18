using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueDashboardQuery : IRequest<LeagueDashboardDto>
{
    public int LeagueId { get; }
    public int? Month { get; }

    public GetLeagueDashboardQuery(int leagueId, int? month = null)
    {
        LeagueId = leagueId;
        Month = month;
    }
}