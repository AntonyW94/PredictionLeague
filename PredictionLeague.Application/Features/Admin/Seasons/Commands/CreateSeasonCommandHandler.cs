using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand>
{
    private readonly ISeasonRepository _seasonRepository;

    public CreateSeasonCommandHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = new Season
        {
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true
        };

        await _seasonRepository.AddAsync(season);
    }
}