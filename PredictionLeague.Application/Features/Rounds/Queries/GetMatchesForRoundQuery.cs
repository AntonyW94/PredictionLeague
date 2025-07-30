using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Rounds.Queries;

public record GetMatchesForRoundQuery(int RoundId) : IRequest<IEnumerable<MatchInRoundDto>>;