using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

public class DeleteSeasonCommandHandler(
    ISeasonRepository seasonRepository,
    ICurrentUserService currentUserService,
    ILogger<DeleteSeasonCommandHandler> logger) : IRequestHandler<DeleteSeasonCommand>
{
    public async Task Handle(DeleteSeasonCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        var hasPredictions = await seasonRepository.HasPredictionsAsync(request.SeasonId, cancellationToken);
        if (hasPredictions)
            throw new BusinessRuleViolationException("Cannot delete a season that has predictions. Remove all predictions first.");

        await seasonRepository.DeleteAsync(request.SeasonId, cancellationToken);

        logger.LogInformation("Season (ID: {SeasonId}) deleted", request.SeasonId);
    }
}
