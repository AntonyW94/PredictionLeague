using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// When a round's predictions close, and whether one match's have - the row-level twins of the rules on the Round and Match
/// entities, for the read paths that hold rows rather than entities.
/// </summary>
public class PredictionWindowTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    #region LatestDeadline

    [Fact]
    public void LatestDeadline_ShouldBeTheRoundDeadline_WhenNoMatchHasItsOwnLockTime()
    {
        LatestOf(Now, null, null).Should().Be(Now);
    }

    [Fact]
    public void LatestDeadline_ShouldBeTheRoundDeadline_WhenThereAreNoMatchesAtAll()
    {
        PredictionWindow.LatestDeadline(Now, []).Should().Be(Now);
    }

    [Fact]
    public void LatestDeadline_ShouldStretchToALaterLockTime()
    {
        // A combined round stays open for its later matches after the deadline that locked the earlier ones.
        LatestOf(Now, Now.AddDays(1)).Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void LatestDeadline_ShouldTakeTheLatestOfSeveralLockTimes()
    {
        LatestOf(Now, Now.AddHours(1), Now.AddDays(2), Now.AddHours(6)).Should().Be(Now.AddDays(2));
    }

    [Fact]
    public void LatestDeadline_ShouldIgnoreAnEarlierLockTime()
    {
        // An early kick-off locks that one match sooner; it does not shorten the round.
        LatestOf(Now, Now.AddHours(-3)).Should().Be(Now);
    }

    [Fact]
    public void LatestDeadline_ShouldIgnoreALockTimeEqualToTheRoundDeadline()
    {
        LatestOf(Now, Now).Should().Be(Now);
    }

    [Fact]
    public void LatestDeadline_ShouldIgnoreMatchesWithNoLockTimeAmongOnesThatHaveThem()
    {
        LatestOf(Now, null, Now.AddDays(1), null).Should().Be(Now.AddDays(1));
    }

    #endregion

    #region HasLocked

    [Fact]
    public void HasLocked_ShouldFollowTheRoundDeadline_ForAMatchWithNoLockTimeOfItsOwn()
    {
        PredictionWindow.HasLocked(null, Now.AddHours(-1), Now).Should().BeTrue();
        PredictionWindow.HasLocked(null, Now.AddHours(1), Now).Should().BeFalse();
    }

    [Fact]
    public void HasLocked_ShouldBeTrue_AtTheDeadlineItself()
    {
        // Inclusive, matching Match.IsPredictionLocked: a match whose deadline is exactly now has locked.
        PredictionWindow.HasLocked(null, Now, Now).Should().BeTrue();
    }

    [Fact]
    public void HasLocked_ShouldPreferTheMatchsOwnLockTime_WhenItComesEarlier()
    {
        // An early kick-off inside a round that is otherwise still open.
        PredictionWindow.HasLocked(Now.AddHours(-1), Now.AddHours(2), Now).Should().BeTrue();
    }

    [Fact]
    public void HasLocked_ShouldPreferTheMatchsOwnLockTime_WhenItComesLater()
    {
        // The round deadline has passed, but this match is still open.
        PredictionWindow.HasLocked(Now.AddHours(1), Now.AddHours(-2), Now).Should().BeFalse();
    }

    #endregion

    private static DateTime LatestOf(DateTime roundDeadlineUtc, params DateTime?[] lockTimes) =>
        PredictionWindow.LatestDeadline(roundDeadlineUtc, lockTimes);
}
