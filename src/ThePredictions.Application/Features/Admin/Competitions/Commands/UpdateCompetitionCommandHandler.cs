using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Admin.Competitions.Commands;

public class UpdateCompetitionCommandHandler(
    ICompetitionRepository competitionRepository,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateCompetitionCommand>
{
    public async Task Handle(UpdateCompetitionCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var competition = await competitionRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, competition, "Competition");

        var existingWithCode = await competitionRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existingWithCode != null && existingWithCode.Id != request.Id)
            throw new InvalidOperationException($"A competition with code '{request.Code}' already exists.");

        competition.UpdateDetails(request.Code, request.Name, request.Type, request.LogoUrl, request.Description, request.ApiLeagueId);

        await competitionRepository.UpdateAsync(competition, cancellationToken);
    }
}
