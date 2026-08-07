using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

public class PrizePreviewBuilderTests
{
    private readonly PrizeEvaluator _evaluator = new();

    private static readonly Dictionary<PrizeType, int> PerEntry = new()
    {
        [PrizeType.Overall] = 8,
        [PrizeType.Round] = 3,
        [PrizeType.MostExactScores] = 2
    };

    private static PrizeSchemeEvaluationRequest Request(int entrants) => new()
    {
        StakePounds = 13,
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
    public void Build_ShouldShowCurrentPrizes_WithTheSettingsSplitAsTheGreenContribution()
    {
        var current = _evaluator.Evaluate(Request(16));
        var projected = _evaluator.Evaluate(Request(17));

        var (breakdown, attribution) = PrizePreviewBuilder.Build(current, projected, PerEntry, 13m);

        // Pot reflects the joiner's projected total; category amounts are the current prizes.
        breakdown.Pot.Should().Be(13 * 17);
        breakdown.Categories.Sum(c => c.SubPot).Should().Be(13 * 16);

        // Every funded category shows its per-entry split, summing to the entry fee.
        breakdown.Categories.Single(c => c.Category == PrizeType.Overall).Delta.Should().Be(8);
        breakdown.Categories.Single(c => c.Category == PrizeType.Round).Delta.Should().Be(3);
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).Delta.Should().Be(2);
        breakdown.Categories.Sum(c => c.Delta ?? 0).Should().Be(13m);

        attribution.Should().ContainSingle();
        attribution[0].Should().StartWith("Your £13 adds");
    }

    [Fact]
    public void Build_ShouldOmitPerSlotDeltas()
    {
        var current = _evaluator.Evaluate(Request(16));
        var projected = _evaluator.Evaluate(Request(17));

        var (breakdown, _) = PrizePreviewBuilder.Build(current, projected, PerEntry, 13m);

        breakdown.Categories.SelectMany(c => c.Slots).Should().OnlyContain(s => s.Delta == null);
    }

    [Fact]
    public void Build_ShouldReturnNoAttribution_WhenNoCategoryFundedByTheEntry()
    {
        var current = _evaluator.Evaluate(Request(16));
        var projected = _evaluator.Evaluate(Request(17));

        var (_, attribution) = PrizePreviewBuilder.Build(current, projected, new Dictionary<PrizeType, int>(), 13m);

        attribution.Should().BeEmpty();
    }

    private static PrizeSchemeEvaluationRequest SingleCategoryRequest(int entrants) => new()
    {
        StakePounds = 13,
        EntrantCount = entrants,
        NumberOfRounds = 38,
        NumberOfMonths = 9,
        Categories = new[] { new PrizeSchemeCategoryInput { Category = PrizeType.Overall, PerEntryPounds = 13 } }
    };

    [Fact]
    public void Build_ShouldReadNaturally_WhenTheEntryFundsASingleCategory()
    {
        // One contribution needs no list punctuation - "adds £13 to Overall.", not "and".
        var perEntry = new Dictionary<PrizeType, int> { [PrizeType.Overall] = 13 };

        var (_, attribution) = PrizePreviewBuilder.Build(
            _evaluator.Evaluate(SingleCategoryRequest(16)),
            _evaluator.Evaluate(SingleCategoryRequest(17)),
            perEntry,
            13m);

        attribution.Should().ContainSingle();
        attribution[0].Should().NotContain(" and ");
        attribution[0].Should().Contain("Overall");
    }

    [Fact]
    public void Build_ShouldJoinTheLastContributionWithAnd()
    {
        var (_, attribution) = PrizePreviewBuilder.Build(
            _evaluator.Evaluate(Request(16)), _evaluator.Evaluate(Request(17)), PerEntry, 13m);

        attribution[0].Should().Contain(", ").And.Contain(" and ");
    }
}
