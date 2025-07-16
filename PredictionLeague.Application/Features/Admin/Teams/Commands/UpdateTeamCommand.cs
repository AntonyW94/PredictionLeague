using MediatR;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class UpdateTeamCommand : UpdateTeamRequest, IRequest
{
    public int Id { get; set; }
}