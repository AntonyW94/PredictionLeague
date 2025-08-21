using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public record FetchRoundsForSeasonQuery(int SeasonId) : IRequest<IEnumerable<RoundWithAllPredictionsInDto>>;