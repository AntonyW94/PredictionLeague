using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonCommandHandler : IRequestHandler<UpdateSeasonCommand>
{
    private readonly ISeasonRepository _seasonRepository;

    public UpdateSeasonCommandHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task Handle(UpdateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.Id);
        Guard.Against.NotFound(request.Id, season, $"Season (ID: {request.Id}) not found during update.");

        season.UpdateDetails(request.Name, request.StartDate, request.EndDate, request.IsActive);

        await _seasonRepository.UpdateAsync(season);
    }
}