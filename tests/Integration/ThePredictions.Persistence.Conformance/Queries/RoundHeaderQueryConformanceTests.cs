using FluentAssertions;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IRoundHeaderQuery"/> implementation must return: one round with its season and competition, or nothing.
/// </summary>
public abstract class RoundHeaderQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IRoundHeaderQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForARoundThatDoesNotExist()
    {
        (await Query.ExecuteAsync(-1, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheRoundWithItsSeasonAndCompetition()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline, displayName: "Quarter Finals");

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - the competition type arrives typed. Both statements this replaces selected it as a number and cast it back.
        round.Should().NotBeNull();
        round!.RoundId.Should().Be(roundId);
        round.RoundNumber.Should().Be(12);
        round.DisplayName.Should().Be("Quarter Finals");
        round.DeadlineUtc.Should().Be(Deadline);
        round.SeasonId.Should().Be(backdrop.SeasonId);
        round.SeasonName.Should().Be("2026/27");
        round.NumberOfRounds.Should().Be(38);
        round.CompetitionType.Should().BeOneOf(CompetitionType.League, CompetitionType.Tournament);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheSeasonsRoundCountSoTheLastRoundCanBeRecognised()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var shortSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "Cup 2026", numberOfRounds: 4);
        var roundId = await Seed.AddRoundAsync(shortSeasonId, 4, Deadline);

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - whether this is the season's last round is a rule, so both numbers come back rather than the answer.
        round!.RoundNumber.Should().Be(4);
        round.NumberOfRounds.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheRoundAskedForAndNoOther()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        round!.RoundNumber.Should().Be(1);
    }
}
