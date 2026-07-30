using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Competitions;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Competitions.Commands;

public class CreateCompetitionCommandHandler(
    ICompetitionRepository competitionRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<CreateCompetitionCommand, CompetitionDto>
{
    public async Task<CompetitionDto> Handle(CreateCompetitionCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var existing = await competitionRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing != null)
            throw new BusinessRuleViolationException($"A competition with code '{request.Code}' already exists.");

        var competition = Competition.Create(request.Code, request.Name, request.Type, request.LogoUrl, request.Description, request.ApiLeagueId, dateTimeProvider);

        var createdCompetition = await competitionRepository.CreateAsync(competition, cancellationToken);

        return new CompetitionDto(
            createdCompetition.Id,
            createdCompetition.Code,
            createdCompetition.Name,
            (int)createdCompetition.Type,
            createdCompetition.LogoUrl,
            createdCompetition.Description,
            createdCompetition.ApiLeagueId,
            0);
    }
}
