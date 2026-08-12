using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public class GetTournamentRoundMappingsQueryHandler(
    ITournamentRoundMappingRepository repository) : IRequestHandler<GetTournamentRoundMappingsQuery, List<TournamentRoundMappingDto>>
{
    public async Task<List<TournamentRoundMappingDto>> Handle(GetTournamentRoundMappingsQuery request, CancellationToken cancellationToken)
    {
        var mappings = await repository.GetBySeasonIdAsync(request.SeasonId, cancellationToken);

        return mappings.Select(m => new TournamentRoundMappingDto
        {
            RoundNumber = m.RoundNumber,
            DisplayName = m.DisplayName,
            // The mapping knows how to read its own stage list, and this had a second copy of that
            // parse - which would have been two answers to one question the day either changed.
            Stages = m.GetStageList(),
            ExpectedMatchCount = m.ExpectedMatchCount
        }).ToList();
    }
}
