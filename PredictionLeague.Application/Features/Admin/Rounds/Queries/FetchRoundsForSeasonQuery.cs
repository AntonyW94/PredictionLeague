using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class FetchRoundsForSeasonQuery : IRequest<IEnumerable<RoundDto>>
{
    public int SeasonId { get; set; }

    public FetchRoundsForSeasonQuery(int seasonId)
    {
        SeasonId = seasonId;
    }
}