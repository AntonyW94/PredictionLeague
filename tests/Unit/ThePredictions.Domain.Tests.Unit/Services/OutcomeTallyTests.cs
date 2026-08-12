using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// How a set of predictions turned out. This existed twice and was tested neither time: as a <c>MERGE</c> with three
/// <c>SUM(CASE WHEN ...)</c> columns writing every player's stored tally, and as a LINQ count in the active-rounds tile
/// for the one player looking at it. The stored numbers feed badges, digests, leaderboards, records and the season recap.
/// </summary>
public class OutcomeTallyTests
{
    [Fact]
    public void For_ShouldCountEachKindOfOutcome()
    {
        // Arrange - deliberately different counts, so a tally reported under the wrong heading cannot pass.
        PredictionOutcome[] outcomes =
        [
            PredictionOutcome.ExactScore,
            PredictionOutcome.CorrectResult,
            PredictionOutcome.CorrectResult,
            PredictionOutcome.Incorrect,
            PredictionOutcome.Incorrect,
            PredictionOutcome.Incorrect
        ];

        // Act
        var counts = OutcomeTally.For(outcomes);

        // Assert
        counts.ExactScoreCount.Should().Be(1);
        counts.CorrectResultCount.Should().Be(2);
        counts.IncorrectCount.Should().Be(3);
    }

    [Fact]
    public void For_ShouldNotCountAPredictionStillWaitingOnItsResult()
    {
        // The old SQL said this as Outcome <> 0, which relied on the reader knowing that the enum's first member is the
        // unjudged one. A pending prediction is not a miss.
        PredictionOutcome[] outcomes = [PredictionOutcome.Pending, PredictionOutcome.ExactScore];

        // Act
        var counts = OutcomeTally.For(outcomes);

        // Assert
        counts.ExactScoreCount.Should().Be(1);
        counts.CorrectResultCount.Should().Be(0);
        counts.IncorrectCount.Should().Be(0);
    }

    [Fact]
    public void For_ShouldCountNothing_WhenThereAreNoPredictions()
    {
        // Act
        var counts = OutcomeTally.For(Array.Empty<PredictionOutcome>());

        // Assert
        counts.Should().Be(new OutcomeCounts(0, 0, 0));
    }

    [Fact]
    public void For_ShouldTreatAnUnpredictedFixtureAsNeitherRightNorWrong()
    {
        // Arrange - the tile's case: it walks the round's fixtures, and one the player left blank has no outcome at all.
        PredictionOutcome?[] outcomes = [null, PredictionOutcome.Incorrect, null];

        // Act
        var counts = OutcomeTally.For(outcomes);

        // Assert
        counts.Should().Be(new OutcomeCounts(0, 0, 1));
    }
}
