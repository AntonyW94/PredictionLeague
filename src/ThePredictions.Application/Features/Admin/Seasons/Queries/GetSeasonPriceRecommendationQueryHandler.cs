using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public class GetSeasonPriceRecommendationQueryHandler(ISeasonPriceRecommendationService recommendationService)
    : IRequestHandler<GetSeasonPriceRecommendationQuery, PriceRecommendationDto>
{
    public async Task<PriceRecommendationDto> Handle(GetSeasonPriceRecommendationQuery request, CancellationToken cancellationToken)
    {
        var recommendation = await recommendationService.RecommendAsync(
            request.CompetitionId,
            request.NumberOfRounds,
            request.StartDateUtc,
            request.SeasonId,
            cancellationToken);

        return new PriceRecommendationDto(
            recommendation.SuggestedStandardPrice,
            recommendation.UnavailableReason,
            recommendation.BusinessBorneAnnualCost,
            recommendation.SeasonRounds,
            recommendation.TotalPaidRoundsInHorizon,
            recommendation.Weight,
            recommendation.ApportionedCost,
            recommendation.BufferRate,
            recommendation.TargetWithBuffer,
            recommendation.ExpectedPlayers,
            recommendation.PerPlayer,
            recommendation.FeeGrossedUp,
            recommendation.FloorApplied);
    }
}
