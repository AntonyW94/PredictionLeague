using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Guards;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonStatusCommandHandler : IRequestHandler<UpdateSeasonStatusCommand>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ICurrentUserService _currentUserService;

    public UpdateSeasonStatusCommandHandler(ISeasonRepository seasonRepository, ICurrentUserService currentUserService)
    {
        _seasonRepository = seasonRepository;
        _currentUserService = currentUserService;
    }

    public async Task Handle(UpdateSeasonStatusCommand request, CancellationToken cancellationToken)
    {
        _currentUserService.EnsureAdministrator();

        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        season.SetIsActive(request.IsActive);

        await _seasonRepository.UpdateAsync(season, cancellationToken);
    }
}