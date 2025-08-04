using MediatR;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public record CreateTeamCommand (
    string Name,
    string LogoUrl,
    string Abbreviation) : IRequest<TeamDto>;