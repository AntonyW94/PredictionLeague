using MediatR;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Application.Features.Predictions.Commands;

public class SubmitPredictionsCommand : SubmitPredictionsRequest, IRequest
{
    public string UserId { get; }

    public SubmitPredictionsCommand(SubmitPredictionsRequest request, string userId)
    {
        RoundId = request.RoundId;
        Predictions = request.Predictions;
        UserId = userId;
    }
}