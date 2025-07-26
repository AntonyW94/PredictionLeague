using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonStatusCommandHandler : IRequestHandler<UpdateSeasonStatusCommand>
{
    private readonly ISeasonRepository _seasonRepository;

    public UpdateSeasonStatusCommandHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task Handle(UpdateSeasonStatusCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        season.SetIsActive(request.IsActive);

        await _seasonRepository.UpdateAsync(season, cancellationToken);
    }
}