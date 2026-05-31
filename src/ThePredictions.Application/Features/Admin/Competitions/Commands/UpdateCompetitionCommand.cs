using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Competitions.Commands;

public record UpdateCompetitionCommand(
    int Id,
    string Code,
    string Name,
    CompetitionType Type,
    string? LogoUrl,
    string? Description,
    int? ApiLeagueId) : IRequest;
