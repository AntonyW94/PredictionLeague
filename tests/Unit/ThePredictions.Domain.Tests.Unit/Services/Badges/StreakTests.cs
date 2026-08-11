using FluentAssertions;
using ThePredictions.Domain.Services.Badges;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Badges;

/// <summary>
/// Runs of consecutive rounds - the On Fire badge. The expectations here were read off the gap-and-island SQL
/// this replaces rather than reasoned out, so they check the behaviour is the same one.
/// </summary>
public class StreakTests
{
    #region Longest

    [Fact]
    public void Longest_ShouldBeNothing_WhenNoRoundsWerePlayed()
    {
        Streak.Longest([]).Should().Be(0);
    }

    [Fact]
    public void Longest_ShouldBeNothing_WhenNoRoundCounted()
    {
        Streak.Longest([false, false, false]).Should().Be(0);
    }

    [Fact]
    public void Longest_ShouldCountASingleRound()
    {
        Streak.Longest([true]).Should().Be(1);
    }

    [Fact]
    public void Longest_ShouldCountConsecutiveRounds()
    {
        Streak.Longest([true, true, true]).Should().Be(3);
    }

    [Fact]
    public void Longest_ShouldBeBrokenByAMissedRound()
    {
        // Not five: the miss ends the run rather than being skipped over.
        Streak.Longest([true, true, false, true, true]).Should().Be(2);
    }

    [Fact]
    public void Longest_ShouldKeepTheBestRun_WhenALaterOneIsShorter()
    {
        Streak.Longest([true, true, true, false, true]).Should().Be(3);
    }

    [Fact]
    public void Longest_ShouldFindTheBestRun_WhenItComesLast()
    {
        Streak.Longest([true, false, true, true, true]).Should().Be(3);
    }

    [Fact]
    public void Longest_ShouldIgnoreLeadingAndTrailingMisses()
    {
        Streak.Longest([false, false, true, true, false, false]).Should().Be(2);
    }

    #endregion

    #region Current

    [Fact]
    public void Current_ShouldBeNothing_WhenNoRoundsWerePlayed()
    {
        Streak.Current([]).Should().Be(0);
    }

    [Fact]
    public void Current_ShouldBeNothing_WhenTheLatestRoundDidNotCount()
    {
        // The whole point of the second metric: a run that has already ended is not a current run, however long it
        // was. This is what drops the badge's second line back to "no current run".
        Streak.Current([true, true, true, false]).Should().Be(0);
    }

    [Fact]
    public void Current_ShouldCountTheRunReachingTheLatestRound()
    {
        Streak.Current([false, true, true]).Should().Be(2);
    }

    [Fact]
    public void Current_ShouldCountEveryRound_WhenNoneWereMissed()
    {
        Streak.Current([true, true, true]).Should().Be(3);
    }

    [Fact]
    public void Current_ShouldIgnoreALongerEarlierRun()
    {
        Streak.Current([true, true, true, false, true]).Should().Be(1);
    }

    #endregion
}
