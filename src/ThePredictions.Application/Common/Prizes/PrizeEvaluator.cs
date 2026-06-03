using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Services.Prizes;

namespace ThePredictions.Application.Common.Prizes;

/// <inheritdoc />
public class PrizeEvaluator : IPrizeEvaluator
{
    public PrizeBreakdownDto Evaluate(PrizeSchemeEvaluationRequest request)
    {
        var allocations = request.Categories
            .Select(c => new PrizeCategoryAllocation
            {
                Category = c.Category,
                Kind = PrizeCategoryRegistry.Definition(c.Category).Kind,
                PerEntryPounds = c.PerEntryPounds,
                RankTable = ResolveRankTable(c)
            })
            .ToList();

        var domainRequest = new PrizeApportionmentRequest
        {
            EntrantCount = request.EntrantCount,
            StakePounds = request.StakePounds,
            AdminTopUpPounds = request.AdminTopUpPounds,
            NumberOfRounds = request.NumberOfRounds,
            NumberOfMonths = request.NumberOfMonths,
            Categories = allocations
        };

        var breakdown = PrizeApportionmentService.Apportion(domainRequest);

        return new PrizeBreakdownDto
        {
            Pot = breakdown.PotPounds,
            EntrantCount = request.EntrantCount,
            Categories = breakdown.Categories
                .Select(c => new PrizeCategoryBreakdownDto
                {
                    Category = c.Category,
                    DisplayName = PrizeCategoryRegistry.Definition(c.Category).DisplayName,
                    Kind = c.Kind,
                    SubPot = c.SubPotPounds,
                    Slots = c.Slots
                        .Select(s => new PrizeSlotDto { Label = s.Label, Amount = s.Amount, Rank = s.Rank, StageName = s.StageName })
                        .ToList()
                })
                .ToList()
        };
    }

    private static RankTable? ResolveRankTable(PrizeSchemeCategoryInput category)
    {
        if (!string.IsNullOrWhiteSpace(category.RankTableJson))
            return RankTableSerializer.Deserialize(category.RankTableJson);

        return PrizeCategoryRegistry.Definition(category.Category).IsRanked ? PrizeCategoryRegistry.DefaultRankTable : null;
    }
}
