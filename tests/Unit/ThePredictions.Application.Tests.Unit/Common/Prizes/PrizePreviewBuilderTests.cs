using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

public class PrizePreviewBuilderTests
{
    private readonly PrizeEvaluator _evaluator = new();

    private static PrizeSchemeEvaluationRequest Request(int entrants) => new()
    {
        StakePounds = 13,
        OverallFivePoundThreshold = 100,
        EntrantCount = entrants,
        NumberOfRounds = 38,
        NumberOfMonths = 9,
        Categories = new[]
        {
            new PrizeSchemeCategoryInput { Category = PrizeType.Overall, PerEntryPounds = 8 },
            new PrizeSchemeCategoryInput { Category = PrizeType.Round, PerEntryPounds = 3 },
            new PrizeSchemeCategoryInput { Category = PrizeType.MostExactScores, PerEntryPounds = 2 }
        }
    };

    [Fact]
    public void Build_ShouldAnnotateCategoryDeltasSummingToStake()
    {
        var current = _evaluator.Evaluate(Request(16));
        var projected = _evaluator.Evaluate(Request(17));

        var (breakdown, attribution) = PrizePreviewBuilder.Build(current, projected, 13m);

        breakdown.Pot.Should().Be(13 * 17);
        breakdown.Categories.Sum(c => c.Delta ?? 0).Should().Be(13m);
        attribution.Should().ContainSingle();
        attribution[0].Should().StartWith("Your £13 adds");
    }

    [Fact]
    public void Build_ShouldGiveNewlyLitSlotItsFullAmountAsDelta()
    {
        // From 1 entrant (single place) to 11 (three places) more Overall ranks light up.
        var current = _evaluator.Evaluate(Request(1));
        var projected = _evaluator.Evaluate(Request(11));

        var (breakdown, _) = PrizePreviewBuilder.Build(current, projected, 13m);

        var overall = breakdown.Categories.Single(c => c.Category == PrizeType.Overall);
        overall.Slots.Should().OnlyContain(s => s.Delta.HasValue);
    }

    [Fact]
    public void Build_ShouldReturnNoAttribution_WhenNoCategoryGrows()
    {
        var current = _evaluator.Evaluate(Request(10));

        var (_, attribution) = PrizePreviewBuilder.Build(current, current, 13m);

        attribution.Should().BeEmpty();
    }
}
