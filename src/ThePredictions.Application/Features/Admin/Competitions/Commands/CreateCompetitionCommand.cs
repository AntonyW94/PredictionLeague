using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Competitions;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Competitions.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record CreateCompetitionCommand(
    string Code,
    string Name,
    CompetitionType Type,
    string? LogoUrl,
    string? Description,
    int? ApiLeagueId) : IRequest<CompetitionDto>;
