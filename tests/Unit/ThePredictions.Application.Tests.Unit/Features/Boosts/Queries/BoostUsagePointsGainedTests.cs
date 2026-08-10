using FluentAssertions;
using ThePredictions.Application.Features.Boosts.Queries;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Boosts.Queries;

/// <summary>
/// What a boost actually won. Previously a <c>CASE</c> expression inside the read:
///
/// <code>
/// CASE WHEN lrr.[Id] IS NOT NULL AND lrr.[HasBoost] = 1
///      THEN lrr.[BoostedPoints] - lrr.[BasePoints] ELSE NULL END
/// </code>
///
/// It is a scoring rule, so it moved to C# with the persistence split. Null and zero mean different things
/// to the page - "not scored yet" against "gained nothing" - which is the part a <c>CASE</c> made easy to
/// get subtly wrong and impossible to unit test.
/// </summary>
public class BoostUsagePointsGainedTests
{
    [Fact]
    public void PointsGained_ShouldBeTheDifference_WhenTheRoundHasBeenScoredWithABoost()
    {
        BoostUsageSummaryBuilder.PointsGained(Usage(hasBoost: true, basePoints: 9, boostedPoints: 18))
            .Should().Be(9);
    }

    [Fact]
    public void PointsGained_ShouldBeZero_WhenTheBoostWonNothing()
    {
        // Zero is a real answer: the boost applied and gained nothing. It must not collapse to null.
        BoostUsageSummaryBuilder.PointsGained(Usage(hasBoost: true, basePoints: 12, boostedPoints: 12))
            .Should().Be(0);
    }

    [Fact]
    public void PointsGained_ShouldBeNull_WhenTheRoundHasNoResultYet()
    {
        // No LeagueRoundResults row: the LEFT JOIN yields nulls, and there are no points to report.
        BoostUsageSummaryBuilder.PointsGained(Usage(hasBoost: false, basePoints: null, boostedPoints: null))
            .Should().BeNull();
    }

    [Fact]
    public void PointsGained_ShouldBeNull_WhenTheRoundWasScoredButNoBoostWasApplied()
    {
        // A result row exists but HasBoost is false, so this usage won nothing attributable to a boost.
        BoostUsageSummaryBuilder.PointsGained(Usage(hasBoost: false, basePoints: 9, boostedPoints: 9))
            .Should().BeNull();
    }

    [Fact]
    public void PointsGained_ShouldBeNull_WhenAScoreIsMissingDespiteTheBoostFlag()
    {
        // Defensive: HasBoost set but the points columns null should not throw or report a wrong figure.
        BoostUsageSummaryBuilder.PointsGained(Usage(hasBoost: true, basePoints: null, boostedPoints: 18))
            .Should().BeNull();
        BoostUsageSummaryBuilder.PointsGained(Usage(hasBoost: true, basePoints: 9, boostedPoints: null))
            .Should().BeNull();
    }

    private static BoostUsageRow Usage(bool hasBoost, int? basePoints, int? boostedPoints) =>
        new("user-1", "DOUBLE_UP", RoundNumber: 7, RoundDeadlineUtc: default,
            HasBoost: hasBoost, BasePoints: basePoints, BoostedPoints: boostedPoints);
}
