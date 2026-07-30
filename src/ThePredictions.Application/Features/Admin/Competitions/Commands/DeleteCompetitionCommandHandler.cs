using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Competitions.Commands;

public class DeleteCompetitionCommandHandler(
    ICompetitionRepository competitionRepository,
    ICurrentUserService currentUserService) : IRequestHandler<DeleteCompetitionCommand>
{
    public async Task Handle(DeleteCompetitionCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var competition = await competitionRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, competition, "Competition");

        if (await competitionRepository.HasSeasonsAsync(request.Id, cancellationToken))
            throw new BusinessRuleViolationException("Cannot delete a competition that still has seasons.");

        await competitionRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
