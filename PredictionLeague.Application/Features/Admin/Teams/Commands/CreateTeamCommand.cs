using MediatR;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class CreateTeamCommand : CreateTeamRequest, IRequest<TeamDto>
{
    public CreateTeamCommand(CreateTeamRequest request)
    {
        Name = request.Name;
        LogoUrl = request.LogoUrl;
    }
}