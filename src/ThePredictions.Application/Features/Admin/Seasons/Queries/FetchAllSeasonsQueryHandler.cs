using MediatR;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>The administrator's list of seasons.</summary>
public class FetchAllSeasonsQueryHandler(ISeasonsQuery seasonsQuery)
    : IRequestHandler<FetchAllSeasonsQuery, IEnumerable<SeasonDto>>
{
    public async Task<IEnumerable<SeasonDto>> Handle(FetchAllSeasonsQuery request, CancellationToken cancellationToken)
    {
        var data = await seasonsQuery.ExecuteAsync(cancellationToken);

        return AdminSeasons.NewestFirst(data.Seasons)
            .Select(season => AdminSeasons.ToDto(season, data))
            .ToList();
    }
}
