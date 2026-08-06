using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class HasSeasonPredictionsQueryHandler(ISeasonRepository seasonRepository)
    : IRequestHandler<HasSeasonPredictionsQuery, bool>
{
    public async Task<bool> Handle(HasSeasonPredictionsQuery request, CancellationToken cancellationToken)
    {
        return await seasonRepository.HasPredictionsAsync(request.SeasonId, cancellationToken);
    }
}
