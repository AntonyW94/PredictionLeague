using MediatR;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Application.Features.Predictions.Queries;

public record GetPredictionPageDataQuery(int RoundId, string UserId) : IRequest<PredictionPageDto>;