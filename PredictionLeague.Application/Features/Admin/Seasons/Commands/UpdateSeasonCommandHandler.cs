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
        var season = await _seasonRepository.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("Season not found.");

        season.Name = request.Name;
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;
        season.IsActive = request.IsActive;

        await _seasonRepository.UpdateAsync(season);
    }
}