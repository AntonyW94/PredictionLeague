using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

public class PrizeEvaluatorTests
{
    private readonly PrizeEvaluator _evaluator = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Evaluate_ShouldProduceConservedBreakdown_UsingDefaultRankTable()
    {
        var request = new PrizeSchemeEvaluationRequest
        {
            StakePounds = 13,
            AdminTopUpPounds = 0,
            EntrantCount = 17,
            NumberOfRounds = 38,
            NumberOfMonths = 9,
            Categories = new[]
            {
                new PrizeSchemeCategoryInput { Category = PrizeType.Overall, PerEntryPounds = 8 },
                new PrizeSchemeCategoryInput { Category = PrizeType.Round, PerEntryPounds = 3 },
                new PrizeSchemeCategoryInput { Category = PrizeType.MostExactScores, PerEntryPounds = 2 }
            }
        };

        var breakdown = _evaluator.Evaluate(request);

        breakdown.Pot.Should().Be(13 * 17);
        breakdown.EntrantCount.Should().Be(17);
        breakdown.Categories.Sum(c => c.SubPot).Should().Be(breakdown.Pot);
        breakdown.Categories.Should().Contain(c => c.Category == PrizeType.Overall && c.DisplayName == "Overall");
        breakdown.Categories.Single(c => c.Category == PrizeType.Round).Slots.Should().Contain(s => s.Label == "Per round");
    }

    [Fact]
    public void Evaluate_ShouldUseRankTableOverride_WhenProvided()
    {
        // Override: a single winner-takes-all band for all entrant counts.
        var overrideJson = "[{\"MinEntrants\":1,\"MaxEntrants\":null,\"Percentages\":[100]}]";

        var request = new PrizeSchemeEvaluationRequest
        {
            StakePounds = 10,
            EntrantCount = 20,
            NumberOfRounds = 38,
            Categories = new[]
            {
                new PrizeSchemeCategoryInput { Category = PrizeType.Overall, PerEntryPounds = 10, RankTableJson = overrideJson }
            }
        };

        var breakdown = _evaluator.Evaluate(request);

        var overall = breakdown.Categories.Single(c => c.Category == PrizeType.Overall);
        overall.Slots.Should().ContainSingle();
        overall.Slots.Single().Amount.Should().Be(200m);
    }

    [Fact]
    public void Evaluate_ShouldMatchDomainServiceOutput_ForSavedScheme()
    {
        var scheme = LeaguePrizeScheme.Create(
            10,
            new[]
            {
                LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 7),
                LeaguePrizeSchemeEntry.Create(PrizeType.MostExactScores, 3)
            },
            "admin-user", isTournament: false, _dateTimeProvider);

        var request = PrizeSchemeEvaluationRequest.FromScheme(scheme, stakePounds: 10, adminTopUpPounds: 0, entrantCount: 25, numberOfRounds: 38, numberOfMonths: 9);
        var breakdown = _evaluator.Evaluate(request);

        breakdown.Pot.Should().Be(250);
        breakdown.Categories.Sum(c => c.SubPot).Should().Be(250);
    }
}
