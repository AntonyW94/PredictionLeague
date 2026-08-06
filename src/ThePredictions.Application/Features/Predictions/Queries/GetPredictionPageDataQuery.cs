using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Predictions;

namespace ThePredictions.Application.Features.Predictions.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetPredictionPageDataQuery(int RoundId, string UserId) : IRequest<PredictionPageDto>;
