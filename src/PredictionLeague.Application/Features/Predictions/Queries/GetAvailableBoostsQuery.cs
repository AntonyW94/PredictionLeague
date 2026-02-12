using MediatR;
using PredictionLeague.Contracts.Boosts;

namespace PredictionLeague.Application.Features.Predictions.Queries;

public record GetAvailableBoostsQuery(int LeagueId, int RoundId, string UserId) : IRequest<List<BoostOptionDto>>;