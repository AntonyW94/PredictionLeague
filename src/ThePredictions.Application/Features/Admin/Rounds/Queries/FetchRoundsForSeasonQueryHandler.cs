using MediatR;
using ThePredictions.Contracts.Admin.Rounds;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>The administrator's list of a season's rounds.</summary>
public class FetchRoundsForSeasonQueryHandler(IAdminSeasonRoundsQuery seasonRoundsQuery)
    : IRequestHandler<FetchRoundsForSeasonQuery, IEnumerable<RoundDto>>
{
    public async Task<IEnumerable<RoundDto>> Handle(FetchRoundsForSeasonQuery request, CancellationToken cancellationToken)
    {
        var rounds = await seasonRoundsQuery.ExecuteAsync(request.SeasonId, cancellationToken);

        // Round number, not deadline: a rescheduled round keeps its place in the season, and the endpoint promises
        // this order.
        return rounds
            .OrderBy(round => round.RoundNumber)
            .Select(AdminRoundMapping.ToDto)
            .ToList();
    }
}
