using MediatR;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public record UpdateTeamCommand(
    int Id,
    string Name,
    string LogoUrl,
    string Abbreviation) : IRequest;