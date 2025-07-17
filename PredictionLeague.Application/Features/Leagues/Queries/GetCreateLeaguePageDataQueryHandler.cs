using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetCreateLeaguePageDataQueryHandler : IRequestHandler<GetCreateLeaguePageDataQuery, CreateLeaguePageData>
{
    private readonly ISeasonRepository _seasonRepository;

    public GetCreateLeaguePageDataQueryHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task<CreateLeaguePageData> Handle(GetCreateLeaguePageDataQuery request, CancellationToken cancellationToken)
    {
        var seasons = await _seasonRepository.GetAllAsync();

        return new CreateLeaguePageData
        {
            Seasons = seasons.Select(s => new SeasonDto
            {
                Id = s.Id,
                Name = s.Name
            }).ToList()
        };
    }
}