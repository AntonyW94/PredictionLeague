using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Predictions;

namespace ThePredictions.Application.Features.Predictions.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record SubmitPredictionsCommand(
    string UserId,
    int RoundId,
    IEnumerable<PredictionSubmissionDto> Predictions) : IRequest, ITransactionalRequest;
