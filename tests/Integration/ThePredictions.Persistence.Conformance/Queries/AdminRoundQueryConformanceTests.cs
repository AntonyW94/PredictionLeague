using FluentAssertions;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IAdminRoundQuery"/> implementation must return: one round, or nothing.
/// </summary>
public abstract class AdminRoundQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IAdminRoundQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForARoundThatDoesNotExist()
    {
        // Act
        var round = await Query.ExecuteAsync(-1, CancellationToken.None);

        // Assert - nothing, rather than an exception. Whether that is a client mistake is the handler's to decide.
        round.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheRoundsDetailsWithItsStatusAsAnEnum()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 4, Deadline, RoundStatus.Published, startDateUtc: Deadline.AddDays(-1));

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        round.Should().NotBeNull();
        round!.Id.Should().Be(roundId);
        round.SeasonId.Should().Be(backdrop.SeasonId);
        round.RoundNumber.Should().Be(4);
        round.StartDateUtc.Should().Be(Deadline.AddDays(-1));
        round.DeadlineUtc.Should().Be(Deadline);
        round.Status.Should().Be(RoundStatus.Published);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnADraftRound()
    {
        // Arrange - the editor is where a draft is turned into a published round, so it has to be readable.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline, RoundStatus.Draft);

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        round!.Status.Should().Be(RoundStatus.Draft);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheRoundsFixtures()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        round!.MatchCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnARoundWithNoFixturesYet()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - a round with nothing in it is still a round. The statement this replaces returned it as one row of
        // null fixture columns, which the mapping then had to recognise and discard.
        round.Should().NotBeNull();
        round!.MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountAnotherRoundsFixtures()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var otherRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddMatchAsync(otherRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        // Act
        var round = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        round!.MatchCount.Should().Be(0);
    }
}
