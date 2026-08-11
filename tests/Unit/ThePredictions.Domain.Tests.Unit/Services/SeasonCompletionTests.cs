using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// When a season has run its course, which three queries wrote as a correlated <c>COUNT(*) &gt;=</c> inside a
/// <c>CASE</c>.
/// </summary>
public class SeasonCompletionTests
{
    [Fact]
    public void IsFinished_ShouldReturnFalse_WhenRoundsRemain()
    {
        SeasonCompletion.IsFinished(completedRoundCount: 37, numberOfRounds: 38).Should().BeFalse();
    }

    [Fact]
    public void IsFinished_ShouldReturnTrue_WhenEveryRoundHasFinished()
    {
        SeasonCompletion.IsFinished(completedRoundCount: 38, numberOfRounds: 38).Should().BeTrue();
    }

    [Fact]
    public void IsFinished_ShouldReturnTrue_WhenMoreRoundsFinishedThanTheSeasonDeclares()
    {
        // The reason the comparison is >= rather than =. Rounds added after the fact would otherwise leave the
        // league showing as in play for ever.
        SeasonCompletion.IsFinished(completedRoundCount: 39, numberOfRounds: 38).Should().BeTrue();
    }

    [Fact]
    public void IsFinished_ShouldReturnFalse_WhenNothingHasFinishedYet()
    {
        SeasonCompletion.IsFinished(completedRoundCount: 0, numberOfRounds: 38).Should().BeFalse();
    }

    [Fact]
    public void IsEveryRoundComplete_ShouldReturnTrue_WhenEveryRoundThatExistsIsComplete()
    {
        SeasonCompletion.IsEveryRoundComplete(roundCount: 3, completedRoundCount: 3).Should().BeTrue();
    }

    [Fact]
    public void IsEveryRoundComplete_ShouldReturnFalse_WhileARoundRemains()
    {
        SeasonCompletion.IsEveryRoundComplete(roundCount: 3, completedRoundCount: 2).Should().BeFalse();
    }

    [Fact]
    public void IsEveryRoundComplete_ShouldReturnFalse_ForASeasonWithNoRoundsAtAll()
    {
        // Without this the payouts screen would offer to pay out a season that has not started.
        SeasonCompletion.IsEveryRoundComplete(roundCount: 0, completedRoundCount: 0).Should().BeFalse();
    }

    [Fact]
    public void TheTwoDefinitionsCanDisagree()
    {
        // A season declaring 38 rounds but holding 40, of which 38 are complete. The dashboards call that finished and
        // the payouts screen does not. Pinned so that the divergence is a documented fact rather than a surprise.
        SeasonCompletion.IsFinished(completedRoundCount: 38, numberOfRounds: 38).Should().BeTrue();
        SeasonCompletion.IsEveryRoundComplete(roundCount: 40, completedRoundCount: 38).Should().BeFalse();
    }
}
