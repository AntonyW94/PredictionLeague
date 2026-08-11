using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// How far through a group of rounds a league is. Both league pickers stated this as three near-identical
/// <c>SUM(CASE WHEN ...)</c> columns over the same table, differing only in what they grouped by.
/// </summary>
public class RoundProgressTests
{
    [Fact]
    public void Of_ShouldCountRoundsCompletedAndRemaining()
    {
        // Arrange
        var statuses = new[]
        {
            RoundStatus.Completed, RoundStatus.Completed, RoundStatus.InProgress, RoundStatus.Published
        };

        // Act
        var progress = RoundProgress.Of(statuses);

        // Assert
        progress.RoundsCompleted.Should().Be(2);
        progress.RoundsRemaining.Should().Be(2);
    }

    [Fact]
    public void Of_ShouldCountADraftAsStillToCome()
    {
        // A period offered because it holds a published round reports its unpublished rounds as remaining, which is
        // what the old SUM(CASE WHEN Status <> @Completed) did.
        var progress = RoundProgress.Of([RoundStatus.Published, RoundStatus.Draft]);

        progress.RoundsRemaining.Should().Be(2);
        progress.RoundsCompleted.Should().Be(0);
    }

    [Fact]
    public void Of_ShouldReportAPeriodAsWorthOffering_WhenAnyRoundIsNotADraft()
    {
        RoundProgress.Of([RoundStatus.Draft, RoundStatus.Published]).HasVisibleRound.Should().BeTrue();
    }

    [Fact]
    public void Of_ShouldReportAPeriodAsNotWorthOffering_WhenEveryRoundIsADraft()
    {
        // Nothing in it exists as far as players are concerned.
        RoundProgress.Of([RoundStatus.Draft, RoundStatus.Draft]).HasVisibleRound.Should().BeFalse();
    }

    [Fact]
    public void Of_ShouldReportNothing_ForNoRoundsAtAll()
    {
        var progress = RoundProgress.Of([]);

        progress.RoundsRemaining.Should().Be(0);
        progress.RoundsCompleted.Should().Be(0);
        progress.HasVisibleRound.Should().BeFalse();
    }
}
