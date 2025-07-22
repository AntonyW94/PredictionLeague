using MediatR;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Queries;

public record GetTeamByIdQuery(int Id) : IRequest<TeamDto?>;
