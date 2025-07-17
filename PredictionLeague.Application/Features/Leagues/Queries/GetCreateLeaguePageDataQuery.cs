using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetCreateLeaguePageDataQuery : IRequest<CreateLeaguePageData>
{
    public bool IsAdmin { get; }

    public GetCreateLeaguePageDataQuery(bool isAdmin)
    {
        IsAdmin = isAdmin;
    }
}