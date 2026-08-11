using MediatR;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>One season, for the administrator's edit screen.</summary>
public class GetSeasonByIdQueryHandler(ISeasonsQuery seasonsQuery)
    : IRequestHandler<GetSeasonByIdQuery, SeasonDto>
{
    public async Task<SeasonDto> Handle(GetSeasonByIdQuery request, CancellationToken cancellationToken)
    {
        var data = await seasonsQuery.ExecuteAsync(cancellationToken);

        var season = data.Seasons.SingleOrDefault(candidate => candidate.Id == request.Id)
                     ?? throw new EntityNotFoundException("Season", request.Id);

        return AdminSeasons.ToDto(season, data);
    }
}
