using MediatR;
using ThePredictions.Contracts.Admin.Competitions;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Competitions.Commands;

public record CreateCompetitionCommand(
    string Code,
    string Name,
    CompetitionType Type,
    string? LogoUrl,
    int? ApiLeagueId) : IRequest<CompetitionDto>;
