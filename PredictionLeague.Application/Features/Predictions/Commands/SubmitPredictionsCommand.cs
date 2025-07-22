using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Application.Features.Predictions.Commands;

public record SubmitPredictionsCommand(
    string UserId,
    int RoundId,
    IEnumerable<PredictionSubmissionDto> Predictions) : IRequest, ITransactionalRequest;