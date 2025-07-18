using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonCommandHandler : IRequestHandler<UpdateSeasonCommand>
{
    private readonly ILogger<UpdateSeasonCommandHandler> _logger;
    private readonly ISeasonRepository _seasonRepository;

    public UpdateSeasonCommandHandler(ILogger<UpdateSeasonCommandHandler> logger, ISeasonRepository seasonRepository)
    {
        _logger = logger;
        _seasonRepository = seasonRepository;
    }

    public async Task Handle(UpdateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.Id);
        if (season == null)
        {
            _logger.LogWarning("Attempted to update non-existent Season (ID: {SeasonId}).", request.Id);
            throw new KeyNotFoundException("Season not found.");
        }

        season.UpdateDetails(request.Name, request.StartDate, request.EndDate, request.IsActive);

        await _seasonRepository.UpdateAsync(season);
    }
}