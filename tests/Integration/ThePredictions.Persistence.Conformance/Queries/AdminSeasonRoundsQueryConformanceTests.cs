using FluentAssertions;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IAdminSeasonRoundsQuery"/> implementation must return: every round of the season, with its
/// fixture count, in no order.
/// </summary>
public abstract class AdminSeasonRoundsQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IAdminSeasonRoundsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForASeasonWithNoRounds()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var rounds = await Query.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsDetailsWithItsStatusAsAnEnum()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 4, Deadline, RoundStatus.Completed, startDateUtc: Deadline.AddDays(-1));

        // Act
        var round = (await Query.ExecuteAsync(backdrop.SeasonId, CancellationToken.None)).Single();

        // Assert - the status arrives typed rather than as the text in the column, so nothing has to parse it back.
        round.Id.Should().Be(roundId);
        round.SeasonId.Should().Be(backdrop.SeasonId);
        round.RoundNumber.Should().Be(4);
        round.StartDateUtc.Should().Be(Deadline.AddDays(-1));
        round.DeadlineUtc.Should().Be(Deadline);
        round.Status.Should().Be(RoundStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfTheSeasonWhateverItsStatus()
    {
        // Arrange - including a draft, because an administrator's list is where a draft is worked on.
        var backdrop = await Seed.AddBackdropAsync();

        var draftId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline, RoundStatus.Draft);
        var publishedId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));
        var completedId = await Seed.AddRoundAsync(backdrop.SeasonId, 3, Deadline.AddDays(14), RoundStatus.Completed);

        // Act
        var rounds = await Query.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        rounds.Select(round => round.Id).Should().BeEquivalentTo([draftId, publishedId, completedId]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnotherSeasonsRounds()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddRoundAsync(otherSeasonId, 1, Deadline.AddYears(1));

        // Act
        var rounds = await Query.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        rounds.Select(round => round.Id).Should().Equal(roundId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountEachRoundsFixtures()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var stockedRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var emptyRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddMatchAsync(stockedRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddMatchAsync(stockedRoundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        // Act
        var rounds = await Query.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert - including the called-off ones, because the count is of the fixtures the round holds rather than the
        // ones anybody can predict.
        rounds.Single(round => round.Id == stockedRoundId).MatchCount.Should().Be(2);
        rounds.Single(round => round.Id == emptyRoundId).MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountAnotherRoundsFixtures()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var firstRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var secondRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddMatchAsync(firstRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        // Act
        var rounds = await Query.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        rounds.Single(round => round.Id == firstRoundId).MatchCount.Should().Be(1);
        rounds.Single(round => round.Id == secondRoundId).MatchCount.Should().Be(0);
    }
}
