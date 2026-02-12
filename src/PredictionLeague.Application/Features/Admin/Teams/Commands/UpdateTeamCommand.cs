using MediatR;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public record UpdateTeamCommand(
    int Id,
    string Name,
    string ShortName,
    string LogoUrl,
    string Abbreviation,
    int? ApiTeamId) : IRequest;