using MediatR;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Domain.Models; 

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class CreateTeamCommand : CreateTeamRequest, IRequest<Team>;