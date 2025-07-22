using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Queries;

public class FetchAllSeasonsQueryHandler : IRequestHandler<FetchAllSeasonsQuery, IEnumerable<SeasonDto>>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IRoundRepository _roundRepository;

    public FetchAllSeasonsQueryHandler(ISeasonRepository seasonRepository, IRoundRepository roundRepository)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
    }

    public async Task<IEnumerable<SeasonDto>> Handle(FetchAllSeasonsQuery request, CancellationToken cancellationToken)
    {
        var seasons = await _seasonRepository.FetchAllAsync(cancellationToken);
        var seasonsToReturn = new List<SeasonDto>();

        foreach (var season in seasons)
        {
            var rounds = await _roundRepository.FetchBySeasonIdAsync(season.Id, cancellationToken);

            seasonsToReturn.Add(new SeasonDto(
                season.Id,
                season.Name,
                season.StartDate,
                season.EndDate,
                season.IsActive,
                rounds.Count()
            ));
        }

        return seasonsToReturn;
    }
}