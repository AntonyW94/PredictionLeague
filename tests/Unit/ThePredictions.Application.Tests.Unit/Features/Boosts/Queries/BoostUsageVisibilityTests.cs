using FluentAssertions;
using ThePredictions.Application.Features.Boosts.Queries;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Boosts.Queries;

/// <summary>
/// The boost secrecy rule: your own boosts always, anyone else's only once that round has closed.
///
/// This rule spent its life as a SQL predicate comparing against <c>GETUTCDATE()</c>. That made it
/// unreachable from a unit test, and meant even the integration test could only arrange deadlines relative
/// to "now" - so the boundary itself was never checked. Both tests below at the exact deadline are things
/// that could not previously be written at all.
/// </summary>
public class BoostUsageVisibilityTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    private const string Me = "user-me";
    private const string Opponent = "user-opponent";

    [Fact]
    public void VisibleTo_ShouldShowMyOwnBoost_WhenTheRoundIsStillOpen()
    {
        var usages = new[] { Usage(Me, roundNumber: 8, deadline: NowUtc.AddDays(3)) };

        BoostUsageVisibility.VisibleTo(usages, Me, NowUtc).Should().HaveCount(1);
    }

    [Fact]
    public void VisibleTo_ShouldHideAnotherPlayersBoost_WhenTheRoundIsStillOpen()
    {
        var usages = new[] { Usage(Opponent, roundNumber: 8, deadline: NowUtc.AddDays(3)) };

        BoostUsageVisibility.VisibleTo(usages, Me, NowUtc).Should().BeEmpty(
            "revealing it while the round is open would let me react to what an opponent has played.");
    }

    [Fact]
    public void VisibleTo_ShouldShowAnotherPlayersBoost_WhenTheRoundHasClosed()
    {
        var usages = new[] { Usage(Opponent, roundNumber: 7, deadline: NowUtc.AddDays(-1)) };

        BoostUsageVisibility.VisibleTo(usages, Me, NowUtc).Should().HaveCount(1,
            "the secrecy is temporary - once the round has locked there is nothing left to react to.");
    }

    [Fact]
    public void IsVisibleTo_ShouldShowAnotherPlayersBoost_WhenTheDeadlineIsExactlyNow()
    {
        // The SQL used <=, so a round whose deadline is exactly now has closed. Pinning the boundary was
        // impossible while the comparison read the database's clock.
        BoostUsageVisibility.IsVisibleTo(Usage(Opponent, 7, NowUtc), Me, NowUtc).Should().BeTrue();
    }

    [Fact]
    public void IsVisibleTo_ShouldHideAnotherPlayersBoost_WhenTheDeadlineIsOneTickAway()
    {
        BoostUsageVisibility.IsVisibleTo(Usage(Opponent, 7, NowUtc.AddTicks(1)), Me, NowUtc).Should().BeFalse();
    }

    [Fact]
    public void VisibleTo_ShouldCensorPerViewer_WhenTwoPlayersLookAtTheSameData()
    {
        // The same set, filtered for each player, rules out a rule that is right for one user only.
        var usages = new[]
        {
            Usage(Me, roundNumber: 8, deadline: NowUtc.AddDays(3)),
            Usage(Opponent, roundNumber: 8, deadline: NowUtc.AddDays(3)),
            Usage(Me, roundNumber: 7, deadline: NowUtc.AddDays(-7)),
            Usage(Opponent, roundNumber: 7, deadline: NowUtc.AddDays(-7))
        };

        BoostUsageVisibility.VisibleTo(usages, Me, NowUtc)
            .Select(u => (u.UserId, u.RoundNumber))
            .Should().BeEquivalentTo([(Me, 8), (Me, 7), (Opponent, 7)],
                "my own open-round boost is mine to see; my opponent's is not until round 8 closes.");

        BoostUsageVisibility.VisibleTo(usages, Opponent, NowUtc)
            .Select(u => (u.UserId, u.RoundNumber))
            .Should().BeEquivalentTo([(Opponent, 8), (Me, 7), (Opponent, 7)]);
    }

    [Fact]
    public void VisibleTo_ShouldReturnEmpty_WhenThereAreNoUsages()
    {
        BoostUsageVisibility.VisibleTo([], Me, NowUtc).Should().BeEmpty();
    }

    private static BoostUsageRow Usage(string userId, int roundNumber, DateTime deadline) =>
        new(userId, "DOUBLE_UP", roundNumber, deadline, HasBoost: true, BasePoints: 9, BoostedPoints: 18);
}
