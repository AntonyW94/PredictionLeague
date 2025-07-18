using MediatR;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Application.Features.Predictions.Queries;

public class GetPredictionPageDataQuery : IRequest<PredictionPageDto>
{
    public int RoundId { get; }
    public string UserId { get; }

    public GetPredictionPageDataQuery(int roundId, string userId)
    {
        RoundId = roundId;
        UserId = userId;
    }
}