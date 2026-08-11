using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// Which round of a season is the one worth showing. This was a <c>ROW_NUMBER() OVER</c> with a <c>CASE</c> inside
/// its <c>ORDER BY</c>, reading the database's clock - so neither the priority order nor the forty-eight hour grace
/// period could be reached by a test.
/// </summary>
public class ActiveRoundTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private sealed record TestRound(int Number, RoundStatus Status, DateTime? CompletedDateUtc = null);

    private static TestRound? ActiveOf(params TestRound[] rounds) =>
        ActiveRound.Of(rounds, Now, r => r.Status, r => r.CompletedDateUtc, r => r.Number);

    [Fact]
    public void Of_ShouldPreferARoundInPlay()
    {
        ActiveOf(
                new TestRound(1, RoundStatus.Completed, Now.AddHours(-1)),
                new TestRound(2, RoundStatus.InProgress),
                new TestRound(3, RoundStatus.Published))!
            .Number.Should().Be(2);
    }

    [Fact]
    public void Of_ShouldPreferARecentlyFinishedRoundOverTheNextOne()
    {
        // So a player checking the site the morning after still sees how their round went.
        ActiveOf(
                new TestRound(1, RoundStatus.Completed, Now.AddHours(-12)),
                new TestRound(2, RoundStatus.Published))!
            .Number.Should().Be(1);
    }

    [Fact]
    public void Of_ShouldMoveOnToTheNextRound_OnceTheGracePeriodHasPassed()
    {
        ActiveOf(
                new TestRound(1, RoundStatus.Completed, Now.AddHours(-49)),
                new TestRound(2, RoundStatus.Published))!
            .Number.Should().Be(2);
    }

    [Fact]
    public void Of_ShouldStillPreferARoundFinishedExactlyInsideTheWindow()
    {
        ActiveOf(
                new TestRound(1, RoundStatus.Completed, Now.AddHours(-48).AddSeconds(1)),
                new TestRound(2, RoundStatus.Published))!
            .Number.Should().Be(1);
    }

    [Fact]
    public void Of_ShouldNotTreatTheBoundaryItselfAsRecent()
    {
        // The old comparison was strictly greater than forty-eight hours ago, and stays that way.
        ActiveOf(
                new TestRound(1, RoundStatus.Completed, Now.AddHours(-48)),
                new TestRound(2, RoundStatus.Published))!
            .Number.Should().Be(2);
    }

    [Fact]
    public void Of_ShouldNeverPickADraftRound()
    {
        // A draft is not yet something players can see.
        ActiveOf(new TestRound(1, RoundStatus.Draft), new TestRound(2, RoundStatus.Published))!
            .Number.Should().Be(2);
    }

    [Fact]
    public void Of_ShouldReturnNothing_WhenEveryRoundIsADraft()
    {
        ActiveOf(new TestRound(1, RoundStatus.Draft)).Should().BeNull();
    }

    [Fact]
    public void Of_ShouldReturnNothing_WhenTheSeasonHasNoRounds()
    {
        ActiveOf().Should().BeNull();
    }

    [Fact]
    public void Of_ShouldFallBackToAnOldFinishedRound_WhenNothingElseIsAvailable()
    {
        // Better the last thing that happened than an empty tile.
        ActiveOf(new TestRound(4, RoundStatus.Completed, Now.AddDays(-30)))!.Number.Should().Be(4);
    }

    [Fact]
    public void Of_ShouldPreferTheLowerRoundNumber_WhenTwoRoundsRankEqually()
    {
        ActiveOf(new TestRound(5, RoundStatus.Published), new TestRound(3, RoundStatus.Published))!
            .Number.Should().Be(3);
    }

    [Fact]
    public void Of_ShouldTreatARoundMarkedCompleteWithNoDateAsOldNews()
    {
        // It cannot claim the grace period, so a published round takes precedence.
        ActiveOf(
                new TestRound(1, RoundStatus.Completed),
                new TestRound(2, RoundStatus.Published))!
            .Number.Should().Be(2);
    }

    [Fact]
    public void IsRecentlyCompleted_ShouldReturnFalse_ForARoundWithNoCompletionDate()
    {
        // Marked complete but with nothing to measure the window from.
        ActiveRound.IsRecentlyCompleted(RoundStatus.Completed, null, Now).Should().BeFalse();
    }

    [Fact]
    public void IsRecentlyCompleted_ShouldReturnFalse_ForARoundThatIsNotCompleted()
    {
        ActiveRound.IsRecentlyCompleted(RoundStatus.InProgress, Now.AddMinutes(-5), Now).Should().BeFalse();
    }
}
