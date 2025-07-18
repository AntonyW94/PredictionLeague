using MediatR;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Queries;

public class GetTeamByIdQuery : IRequest<TeamDto?>
{
    public int Id { get; }

    public GetTeamByIdQuery(int teamId)
    {
        Id = teamId;
    }
}
