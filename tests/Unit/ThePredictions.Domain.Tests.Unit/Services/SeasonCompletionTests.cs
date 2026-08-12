using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// Whether a season has run its course. There were two answers to this, and they could disagree - the dashboards counted
/// against the length the season declares, the payouts screen against the rounds that exist. This is the only one now, and
/// it is the second: an administrator's typed number is not the authority on how many rounds a season turned out to have.
/// </summary>
public class SeasonCompletionTests
{
    [Fact]
    public void IsEveryRoundComplete_ShouldReturnTrue_WhenEveryRoundThatExistsIsComplete()
    {
        SeasonCompletion.IsEveryRoundComplete(roundCount: 3, completedRoundCount: 3).Should().BeTrue();
    }

    [Fact]
    public void IsEveryRoundComplete_ShouldReturnFalse_WhenARoundIsStillToBePlayed()
    {
        SeasonCompletion.IsEveryRoundComplete(roundCount: 38, completedRoundCount: 37).Should().BeFalse();
    }

    [Fact]
    public void IsEveryRoundComplete_ShouldReturnFalse_WhenNothingHasFinishedYet()
    {
        SeasonCompletion.IsEveryRoundComplete(roundCount: 38, completedRoundCount: 0).Should().BeFalse();
    }

    /// <summary>
    /// The half of the rule that is easy to leave out: without it an empty season reports itself finished, and the payouts
    /// screen offers to settle a season that has not started.
    /// </summary>
    [Fact]
    public void IsEveryRoundComplete_ShouldReturnFalse_WhenTheSeasonHasNoRoundsAtAll()
    {
        SeasonCompletion.IsEveryRoundComplete(roundCount: 0, completedRoundCount: 0).Should().BeFalse();
    }

    /// <summary>
    /// The case that made the old pair disagree. A season declaring 38 rounds but holding 40, of which 38 are complete, was
    /// "finished" by the definition the dashboards used - which is how a season with two rounds still to play could have
    /// been offered for payout.
    /// </summary>
    [Fact]
    public void IsEveryRoundComplete_ShouldReturnFalse_WhenTheSeasonHoldsMoreRoundsThanItDeclares()
    {
        SeasonCompletion.IsEveryRoundComplete(roundCount: 40, completedRoundCount: 38).Should().BeFalse();
    }
}
