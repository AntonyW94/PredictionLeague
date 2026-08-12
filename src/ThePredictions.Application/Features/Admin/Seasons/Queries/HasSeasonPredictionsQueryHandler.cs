using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public class HasSeasonPredictionsQueryHandler(ISeasonRepository seasonRepository)
    : IRequestHandler<HasSeasonPredictionsQuery, bool>
{
    public async Task<bool> Handle(HasSeasonPredictionsQuery request, CancellationToken cancellationToken)
    {
        return await seasonRepository.HasPredictionsAsync(request.SeasonId, cancellationToken);
    }
}
