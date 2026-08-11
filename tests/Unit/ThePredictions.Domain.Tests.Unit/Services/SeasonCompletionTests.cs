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
}
