using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
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
            Stages = m.Stages
                .Split('|')
                .Where(s => Enum.TryParse<TournamentStage>(s, out _))
                .Select(s => Enum.Parse<TournamentStage>(s))
                .ToList(),
            ExpectedMatchCount = m.ExpectedMatchCount
        }).ToList();
    }
}
