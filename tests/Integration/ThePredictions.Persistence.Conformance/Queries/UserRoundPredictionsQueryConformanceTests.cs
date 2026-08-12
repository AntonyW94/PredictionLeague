using FluentAssertions;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IUserRoundPredictionsQuery"/> implementation must return: what one player predicted in one round, and
/// nothing else.
/// </summary>
public abstract class UserRoundPredictionsQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IUserRoundPredictionsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenTheyPredictedNothing()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        // Act
        var predictions = await Query.ExecuteAsync(backdrop.UserId, roundId, CancellationToken.None);

        // Assert - no row rather than a row of nulls. Which fixtures still need filling in is the caller's to work out from
        // the fixtures themselves.
        predictions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheScoresAndTheOutcome()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var matchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        await Seed.AddPredictionAsync(matchId, backdrop.UserId, homeScore: 3, awayScore: 1, PredictionOutcome.ExactScore);

        // Act
        var prediction = (await Query.ExecuteAsync(backdrop.UserId, roundId, CancellationToken.None)).Single();

        // Assert - the outcome is here because the share card colours the pick by it, while the prediction form ignores it.
        prediction.MatchId.Should().Be(matchId);
        prediction.PredictedHomeScore.Should().Be(3);
        prediction.PredictedAwayScore.Should().Be(1);
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAPredictionThatHasNotBeenScoredYet()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var matchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        await Seed.AddPredictionAsync(matchId, backdrop.UserId);

        // Act
        var prediction = (await Query.ExecuteAsync(backdrop.UserId, roundId, CancellationToken.None)).Single();

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnotherPlayersPredictions()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var matchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        await Seed.AddPredictionAsync(matchId, otherUserId);

        // Act
        var predictions = await Query.ExecuteAsync(backdrop.UserId, roundId, CancellationToken.None);

        // Assert - a prediction form showing somebody else's picks is the worst thing this read could do.
        predictions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnPredictionsFromAnotherRound()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var otherRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        var matchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var otherMatchId = await Seed.AddMatchAsync(otherRoundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        await Seed.AddPredictionAsync(matchId, backdrop.UserId);
        await Seed.AddPredictionAsync(otherMatchId, backdrop.UserId);

        // Act
        var predictions = await Query.ExecuteAsync(backdrop.UserId, roundId, CancellationToken.None);

        // Assert
        predictions.Select(prediction => prediction.MatchId).Should().Equal(matchId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAPredictionForEveryFixtureTheyHaveEntered()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        var firstMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var secondMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        await Seed.AddPredictionAsync(firstMatchId, backdrop.UserId);
        await Seed.AddPredictionAsync(secondMatchId, backdrop.UserId);

        // Act
        var predictions = await Query.ExecuteAsync(backdrop.UserId, roundId, CancellationToken.None);

        // Assert
        predictions.Select(prediction => prediction.MatchId).Should().BeEquivalentTo([firstMatchId, secondMatchId]);
    }
}
