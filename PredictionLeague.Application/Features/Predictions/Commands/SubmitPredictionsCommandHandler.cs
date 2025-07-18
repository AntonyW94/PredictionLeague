using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Predictions.Commands;

public class SubmitPredictionsCommandHandler : IRequestHandler<SubmitPredictionsCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IUserPredictionRepository _predictionRepository;

    public SubmitPredictionsCommandHandler(IRoundRepository roundRepository, IUserPredictionRepository predictionRepository)
    {
        _roundRepository = roundRepository;
        _predictionRepository = predictionRepository;
    }

    public async Task Handle(SubmitPredictionsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId) ?? throw new KeyNotFoundException("The specified round could not be found.");
        if (round.Deadline < DateTime.UtcNow)
            throw new InvalidOperationException("The prediction deadline has passed for this round.");

        foreach (var predictionDto in request.Predictions)
        {
            var prediction = new UserPrediction
            {
                MatchId = predictionDto.MatchId,
                UserId = request.UserId,
                PredictedHomeScore = predictionDto.PredictedHomeScore,
                PredictedAwayScore = predictionDto.PredictedAwayScore
            };
            
            await _predictionRepository.UpsertAsync(prediction);
        }
    }
}