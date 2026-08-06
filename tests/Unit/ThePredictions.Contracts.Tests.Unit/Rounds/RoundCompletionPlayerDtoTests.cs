using FluentAssertions;
using ThePredictions.Contracts.Rounds;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Rounds;

public class RoundCompletionPlayerDtoTests
{
    private static RoundCompletionPlayerDto Player(int predictedCount, int missingCount) =>
        new("user-1", "Alex Player", "alex@example.com", predictedCount, null,
            Enumerable.Range(1, missingCount)
                .Select(i => new MissingFixtureDto(i, i, $"Home {i}", $"Away {i}"))
                .ToList());

    [Fact]
    public void MissingCount_ShouldCountTheMissingFixtures()
    {
        Player(predictedCount: 3, missingCount: 2).MissingCount.Should().Be(2);
    }

    [Fact]
    public void MissingCount_ShouldBeZero_WhenNothingIsMissing()
    {
        Player(predictedCount: 5, missingCount: 0).MissingCount.Should().Be(0);
    }

    [Fact]
    public void IsPartial_ShouldBeTrue_WhenSomeButNotAllFixturesAreEntered()
    {
        Player(predictedCount: 3, missingCount: 2).IsPartial.Should().BeTrue();
    }

    [Fact]
    public void IsPartial_ShouldBeFalse_WhenEverythingIsEntered()
    {
        Player(predictedCount: 5, missingCount: 0).IsPartial.Should().BeFalse();
    }

    [Fact]
    public void IsPartial_ShouldBeFalse_WhenNothingIsEntered()
    {
        Player(predictedCount: 0, missingCount: 5).IsPartial.Should().BeFalse();
    }

    [Fact]
    public void HasEnteredNothing_ShouldBeTrue_WhenThereAreFixturesAndNonePredicted()
    {
        Player(predictedCount: 0, missingCount: 5).HasEnteredNothing.Should().BeTrue();
    }

    [Fact]
    public void HasEnteredNothing_ShouldBeFalse_WhenSomethingIsEntered()
    {
        Player(predictedCount: 1, missingCount: 4).HasEnteredNothing.Should().BeFalse();
    }

    [Fact]
    public void HasEnteredNothing_ShouldBeFalse_WhenThereIsNothingLeftToEnter()
    {
        // A player with no predictions and no missing fixtures has nothing outstanding, so this
        // must not read as "entered nothing" - that would chase them for a complete round.
        Player(predictedCount: 0, missingCount: 0).HasEnteredNothing.Should().BeFalse();
    }

    [Fact]
    public void APlayerWhoIsDoneShouldBeNeitherPartialNorEmpty()
    {
        var player = Player(predictedCount: 8, missingCount: 0);

        player.IsPartial.Should().BeFalse();
        player.HasEnteredNothing.Should().BeFalse();
        player.MissingCount.Should().Be(0);
    }

    [Fact]
    public void TwoPlayersSharingEveryValueShouldBeEqual()
    {
        // The fixture list must be the same instance: a record compares collection members by
        // reference, so two separately-built lists would make identical players unequal.
        IReadOnlyList<MissingFixtureDto> fixtures = [new(1, 1, "Home", "Away")];

        var first = new RoundCompletionPlayerDto("user-1", "Alex Player", "alex@example.com", 3, null, fixtures);
        var second = new RoundCompletionPlayerDto("user-1", "Alex Player", "alex@example.com", 3, null, fixtures);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void PlayersWithSeparateButEquivalentFixtureListsShouldNotBeEqual()
    {
        Player(3, 2).Should().NotBe(Player(3, 2));
    }

    [Fact]
    public void PlayersDifferingInProgressShouldNotBeEqual()
    {
        Player(3, 2).Should().NotBe(Player(4, 1));
    }

    [Fact]
    public void WithShouldCopyThePlayerAndChangeOnlyTheNamedField()
    {
        var original = Player(3, 2);

        var copy = original with { PredictedCount = 5 };

        copy.PredictedCount.Should().Be(5);
        copy.PlayerName.Should().Be(original.PlayerName);
    }

    [Fact]
    public void ToStringShouldIncludeTheIdentifyingFields()
    {
        Player(3, 2).ToString().Should().Contain("user-1").And.Contain("Alex Player");
    }
}
