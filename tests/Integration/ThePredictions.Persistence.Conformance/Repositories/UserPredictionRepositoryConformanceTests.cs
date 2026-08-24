using FluentAssertions;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Common;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Repositories;

/// <summary>
/// What any <see cref="IUserPredictionRepository"/> implementation must do with the one table holding what
/// players actually typed in.
///
/// Both writes here store several rows at once - a whole round submitted at the deadline, and the outcomes
/// scoring applies to every prediction on a fixture whose score moved - so both are one statement over a set
/// rather than a statement per row. Neither had a test of any kind, which for a set-based rewrite is the
/// wrong place to have a gap: the failure mode is a column silently written with the wrong value or none,
/// which no unit test over a mocked connection can see.
/// </summary>
public abstract class UserPredictionRepositoryConformanceTests
{
    /// <summary>The adapter's repository, freshly built, with no transaction in progress.</summary>
    protected abstract IUserPredictionRepository Repository { get; }

    /// <summary>Direct writes, bypassing the repository.</summary>
    protected abstract ITestDataSeeder Seed { get; }

    /// <summary>Direct reads, bypassing the repository.</summary>
    protected abstract ITestDataInspector Inspect { get; }

    private static readonly DateTime SubmittedAt = new(2026, 8, 12, 18, 30, 0, DateTimeKind.Utc);

    #region Submitting predictions

    [Fact]
    public async Task UpsertBatchAsync_ShouldStoreEveryPredictionInTheBatch()
    {
        // The whole point: a player submits a round, not a fixture. One round trip per fixture is what this
        // replaced, and a statement that stored only the first row would look identical from the outside.
        var world = await ArrangeAsync(fixtureCount: 3);

        // Act
        await Repository.UpsertBatchAsync(
            world.MatchIds.Select((matchId, index) => Prediction(matchId, world.UserId, index, 0)).ToList(),
            CancellationToken.None);

        // Assert
        foreach (var (matchId, index) in world.MatchIds.Select((id, i) => (id, i)))
        {
            var stored = await Inspect.PredictionAsync(matchId, world.UserId);
            stored.Should().NotBeNull($"fixture {index + 1} of {world.MatchIds.Count} was in the batch.");
            stored!.PredictedHomeScore.Should().Be(index);
            stored.PredictedAwayScore.Should().Be(0);
        }
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldStoreThePendingOutcomeOnANewPrediction()
    {
        // Outcome is stored as the enum's underlying number rather than its name, so a rewrite that let the
        // serialiser choose the form would write "Pending" into an int column - or, worse, a different number.
        var world = await ArrangeAsync(fixtureCount: 1);

        // Act
        await Repository.UpsertBatchAsync([Prediction(world.MatchIds[0], world.UserId, 2, 1)], CancellationToken.None);

        // Assert
        var stored = await Inspect.PredictionAsync(world.MatchIds[0], world.UserId);
        stored!.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldOverwriteAPredictionThePlayerHasAlreadyMade()
    {
        // Arrange
        var world = await ArrangeAsync(fixtureCount: 1);
        await Seed.AddPredictionAsync(world.MatchIds[0], world.UserId, homeScore: 0, awayScore: 0);

        // Act
        await Repository.UpsertBatchAsync([Prediction(world.MatchIds[0], world.UserId, 3, 1)], CancellationToken.None);

        // Assert
        var stored = await Inspect.PredictionAsync(world.MatchIds[0], world.UserId);
        stored!.PredictedHomeScore.Should().Be(3);
        stored.PredictedAwayScore.Should().Be(1);
        stored.UpdatedAtUtc.Should().Be(SubmittedAt, "the timestamp comes from the entity, not the database's clock.");
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldLeaveAnotherPlayersPredictionForTheSameFixtureAlone()
    {
        // The match on its own is not the key - two players predict the same fixture - so a source joined on
        // the fixture alone would overwrite somebody else's answer.
        var world = await ArrangeAsync(fixtureCount: 1);
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddPredictionAsync(world.MatchIds[0], otherUserId, homeScore: 5, awayScore: 5);

        // Act
        await Repository.UpsertBatchAsync([Prediction(world.MatchIds[0], world.UserId, 1, 0)], CancellationToken.None);

        // Assert
        var theirs = await Inspect.PredictionAsync(world.MatchIds[0], otherUserId);
        theirs!.PredictedHomeScore.Should().Be(5);
        theirs.PredictedAwayScore.Should().Be(5);
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldStoreNothing_WhenThereIsNothingToStore()
    {
        var world = await ArrangeAsync(fixtureCount: 1);

        // Act
        var act = async () => await Repository.UpsertBatchAsync([], CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await Inspect.PredictionAsync(world.MatchIds[0], world.UserId)).Should().BeNull();
    }

    #endregion

    #region Scoring them

    [Fact]
    public async Task UpdateOutcomesAsync_ShouldStoreTheOutcomeOfEveryPredictionInTheBatch()
    {
        // Arrange - two fixtures predicted, both scored in one pass.
        var world = await ArrangeAsync(fixtureCount: 2);
        await Repository.UpsertBatchAsync(
            [Prediction(world.MatchIds[0], world.UserId, 2, 1), Prediction(world.MatchIds[1], world.UserId, 0, 0)],
            CancellationToken.None);

        var stored = (await Repository.GetByMatchIdsAsync(world.MatchIds, CancellationToken.None)).ToList();
        var exact = stored.Single(prediction => prediction.MatchId == world.MatchIds[0]);
        var wrong = stored.Single(prediction => prediction.MatchId == world.MatchIds[1]);

        exact.SetOutcome(MatchStatus.Completed, 2, 1, new FixedClock(SubmittedAt.AddHours(3)));
        wrong.SetOutcome(MatchStatus.Completed, 4, 0, new FixedClock(SubmittedAt.AddHours(3)));

        // Act
        await Repository.UpdateOutcomesAsync([exact, wrong], CancellationToken.None);

        // Assert
        (await Inspect.PredictionAsync(world.MatchIds[0], world.UserId))!.Outcome.Should().Be(PredictionOutcome.ExactScore);
        (await Inspect.PredictionAsync(world.MatchIds[1], world.UserId))!.Outcome.Should().Be(PredictionOutcome.Incorrect);
    }

    [Fact]
    public async Task UpdateOutcomesAsync_ShouldStampTheTimeTheEntityDecidedOn()
    {
        // The statement used to write GETUTCDATE() here, overwriting the entity's own answer with the
        // database's. It is asserted rather than described because nothing else here would notice.
        var world = await ArrangeAsync(fixtureCount: 1);
        await Repository.UpsertBatchAsync([Prediction(world.MatchIds[0], world.UserId, 2, 1)], CancellationToken.None);

        var scoredAt = SubmittedAt.AddHours(3);
        var prediction = (await Repository.GetByMatchIdsAsync(world.MatchIds, CancellationToken.None)).Single();
        prediction.SetOutcome(MatchStatus.Completed, 2, 1, new FixedClock(scoredAt));

        // Act
        await Repository.UpdateOutcomesAsync([prediction], CancellationToken.None);

        // Assert
        (await Inspect.PredictionAsync(world.MatchIds[0], world.UserId))!.UpdatedAtUtc.Should().Be(scoredAt);
    }

    [Fact]
    public async Task UpdateOutcomesAsync_ShouldLeaveAPredictionAbsentFromTheBatchAlone()
    {
        // Arrange
        var world = await ArrangeAsync(fixtureCount: 2);
        await Repository.UpsertBatchAsync(
            [Prediction(world.MatchIds[0], world.UserId, 2, 1), Prediction(world.MatchIds[1], world.UserId, 0, 0)],
            CancellationToken.None);

        var scored = (await Repository.GetByMatchIdsAsync([world.MatchIds[0]], CancellationToken.None)).Single();
        scored.SetOutcome(MatchStatus.Completed, 2, 1, new FixedClock(SubmittedAt.AddHours(3)));

        // Act
        await Repository.UpdateOutcomesAsync([scored], CancellationToken.None);

        // Assert - the fixture that has not been played keeps its pending outcome.
        (await Inspect.PredictionAsync(world.MatchIds[1], world.UserId))!.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public async Task UpdateOutcomesAsync_ShouldChangeNothing_WhenThereIsNothingToStore()
    {
        var world = await ArrangeAsync(fixtureCount: 1);
        await Repository.UpsertBatchAsync([Prediction(world.MatchIds[0], world.UserId, 2, 1)], CancellationToken.None);

        // Act
        var act = async () => await Repository.UpdateOutcomesAsync([], CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await Inspect.PredictionAsync(world.MatchIds[0], world.UserId))!.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    #endregion

    private static UserPrediction Prediction(int matchId, string userId, int homeScore, int awayScore) =>
        UserPrediction.Create(userId, matchId, homeScore, awayScore, new FixedClock(SubmittedAt));

    /// <summary>One round of fixtures for the seeded player to predict.</summary>
    private async Task<PredictionWorld> ArrangeAsync(int fixtureCount)
    {
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(2));

        var matchIds = new List<int>();
        for (var index = 0; index < fixtureCount; index++)
        {
            matchIds.Add(await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId));
        }

        return new PredictionWorld(roundId, matchIds, backdrop.UserId);
    }

    private sealed record PredictionWorld(int RoundId, List<int> MatchIds, string UserId);

    /// <summary>
    /// Declared here rather than taken from the shared test helpers: this project references Application and
    /// nothing else, and <c>LayerDependencyConventionTests</c> pins that so the suite cannot quietly become
    /// specific to one adapter. A one-property clock is a smaller price than loosening the rule.
    /// </summary>
    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
