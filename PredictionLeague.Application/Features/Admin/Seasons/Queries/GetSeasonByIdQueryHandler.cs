using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Queries;

public class GetSeasonByIdQueryHandler : IRequestHandler<GetSeasonByIdQuery, SeasonDto?>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IRoundRepository _roundRepository;

    public GetSeasonByIdQueryHandler(ISeasonRepository seasonRepository, IRoundRepository roundRepository)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
    }
    
    public async Task<SeasonDto?> Handle(GetSeasonByIdQuery request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.Id);
        if (season == null)
            return null;

        var rounds = await _roundRepository.GetBySeasonIdAsync(request.Id);
       
        return new SeasonDto
        {
            Id = season.Id,
            Name = season.Name,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            IsActive = season.IsActive,
            RoundCount = rounds.Count()
        };
    }
}