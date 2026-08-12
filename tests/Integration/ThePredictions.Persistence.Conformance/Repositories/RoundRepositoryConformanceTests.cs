using FluentAssertions;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Repositories;

/// <summary>
/// What any <see cref="IRoundRepository"/> implementation must do, whatever database it speaks to.
///
/// The rule that matters most here is the delete guard. <c>UpdateAsync</c> works out which matches to
/// insert, update and delete by diffing the incoming round against what is stored, and a match that
/// players have already predicted must survive removal. It is a data-loss guard: the schema cascades a
/// match delete to its predictions, so getting this wrong destroys player data with no error and no trace.
/// <see cref="Predictions_ShouldBeCascadeDeleted_WhenAMatchIsDeletedWithoutTheGuard"/> pins that premise,
/// so the guard's importance is demonstrated rather than asserted in a comment.
///
/// None of it is reachable by a unit test: the rule lives in a SQL predicate, and a mocked connection only
/// proves a string was handed over. And none of it is specific to SQL Server - a second adapter has to
/// implement the same guard over the same cascade, so these tests are the contract, not a description of
/// one implementation. Derive from this class, supply the three members below, and the suite runs against
/// that adapter unchanged.
/// </summary>
public abstract class RoundRepositoryConformanceTests
{
    /// <summary>The adapter's repository, freshly built, with no transaction in progress.</summary>
    protected abstract IRoundRepository Repository { get; }

    /// <summary>Direct writes, bypassing the repository.</summary>
    protected abstract ITestDataSeeder Seed { get; }

    /// <summary>Direct reads, bypassing the repository.</summary>
    protected abstract ITestDataInspector Inspect { get; }

    #region The delete guard

    [Fact]
    public async Task UpdateAsync_ShouldLeaveTheMatchInPlace_WhenAMatchWithPredictionsIsRemovedFromTheRound()
    {
        // Arrange - two fixtures in a round; a player has predicted the second.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var keptMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var predictedMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);
        await Seed.AddPredictionAsync(predictedMatchId, backdrop.UserId);

        // Act - the admin removes the predicted fixture from the round.
        var round = RoundWith(roundId, backdrop.SeasonId, [ExistingMatch(keptMatchId, roundId, backdrop)]);
        await Repository.UpdateAsync(round, CancellationToken.None);

        // Assert
        var remaining = await Inspect.MatchIdsForRoundAsync(roundId);
        remaining.Should().BeEquivalentTo(new[] { keptMatchId, predictedMatchId },
            "a fixture players have already predicted must survive the edit.");

        (await Inspect.PredictionCountForMatchAsync(predictedMatchId)).Should().Be(1,
            "the predictions are what the guard exists to protect.");
    }

    [Fact]
    public async Task UpdateAsync_ShouldDeleteTheMatch_WhenARemovedMatchHasNoPredictions()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var keptMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var unpredictedMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        // Act
        var round = RoundWith(roundId, backdrop.SeasonId, [ExistingMatch(keptMatchId, roundId, backdrop)]);
        await Repository.UpdateAsync(round, CancellationToken.None);

        // Assert - the guard protects predictions, not fixtures, so this one goes.
        var remaining = await Inspect.MatchIdsForRoundAsync(roundId);
        remaining.Should().BeEquivalentTo(new[] { keptMatchId });
        remaining.Should().NotContain(unpredictedMatchId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldDeleteOnlyTheUnpredictedMatches_WhenSeveralAreRemovedAtOnce()
    {
        // Arrange - two removals in one call, one of them predicted. The delete runs as a single statement
        // over both ids, so the guard has to discriminate row by row.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var keptMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var predictedMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);
        var unpredictedMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddPredictionAsync(predictedMatchId, backdrop.UserId);

        // Act
        var round = RoundWith(roundId, backdrop.SeasonId, [ExistingMatch(keptMatchId, roundId, backdrop)]);
        await Repository.UpdateAsync(round, CancellationToken.None);

        // Assert
        var remaining = await Inspect.MatchIdsForRoundAsync(roundId);
        remaining.Should().BeEquivalentTo(new[] { keptMatchId, predictedMatchId });
        remaining.Should().NotContain(unpredictedMatchId);
    }

    [Fact]
    public async Task Predictions_ShouldBeCascadeDeleted_WhenAMatchIsDeletedWithoutTheGuard()
    {
        // Arrange - this asserts the adapter's schema, not its repository. It is here because it is the
        // reason the guard exists: without it a removed fixture silently takes its predictions.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var matchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddPredictionAsync(matchId, backdrop.UserId);

        // Act - an unguarded delete, as UpdateAsync would issue if its guard were dropped.
        await Seed.DeleteMatchAsync(matchId);

        // Assert
        (await Inspect.PredictionCountForMatchAsync(matchId)).Should().Be(0,
            "the predictions foreign key cascades, so the delete succeeds and the predictions vanish "
            + "without an error. That is what makes the repository's guard load-bearing, and any adapter "
            + "whose schema does not cascade here would be storing orphaned predictions instead.");
    }

    #endregion

    #region Ordering against MoveMatchesToRoundAsync

    [Fact]
    public async Task UpdateAsync_ShouldNotDeleteAMovedMatch_WhenTheMoveRanFirst()
    {
        // Arrange - two rounds; the second fixture is moved out of round 1 before the update.
        var backdrop = await Seed.AddBackdropAsync();
        var sourceRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var targetRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 2, deadlineUtc: DateTime.UtcNow.AddDays(9));
        var stayingMatchId = await Seed.AddMatchAsync(sourceRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var movedMatchId = await Seed.AddMatchAsync(sourceRoundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        var repository = Repository;

        // Act
        await repository.MoveMatchesToRoundAsync([movedMatchId], targetRoundId, CancellationToken.None);
        var round = RoundWith(sourceRoundId, backdrop.SeasonId, [ExistingMatch(stayingMatchId, sourceRoundId, backdrop)]);
        await repository.UpdateAsync(round, CancellationToken.None);

        // Assert - the move took the fixture out of the round before the diff was computed, so the diff
        // never saw it as removed.
        (await Inspect.RoundIdForMatchAsync(movedMatchId)).Should().Be(targetRoundId,
            "the moved fixture must survive the update of the round it left.");
    }

    [Fact]
    public async Task UpdateAsync_ShouldDeleteAMatchDestinedForAnotherRound_WhenTheMoveRunsAfterwards()
    {
        // Arrange - the same scenario in the wrong order. This is not desirable behaviour; it is the
        // ordering dependency written down, so a caller reordering the two calls fails here rather than in
        // production.
        var backdrop = await Seed.AddBackdropAsync();
        var sourceRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var targetRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 2, deadlineUtc: DateTime.UtcNow.AddDays(9));
        var stayingMatchId = await Seed.AddMatchAsync(sourceRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var matchToMoveId = await Seed.AddMatchAsync(sourceRoundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        var repository = Repository;

        // Act
        var round = RoundWith(sourceRoundId, backdrop.SeasonId, [ExistingMatch(stayingMatchId, sourceRoundId, backdrop)]);
        await repository.UpdateAsync(round, CancellationToken.None);
        await repository.MoveMatchesToRoundAsync([matchToMoveId], targetRoundId, CancellationToken.None);

        // Assert
        (await Inspect.MatchExistsAsync(matchToMoveId)).Should().BeFalse(
            "UpdateAsync treats a match absent from the incoming round as removed, so MoveMatchesToRoundAsync "
            + "must run before it - see UpdateRoundCommandHandler.");
    }

    #endregion

    #region Insert, update and delete together

    [Fact]
    public async Task UpdateAsync_ShouldInsertUpdateAndDeleteTogether_WhenOneCallDoesAllThree()
    {
        // Arrange - three fixtures: one to be edited, one resubmitted unchanged, one to be removed.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var editedMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId, matchNumber: 1);
        var secondMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId, matchNumber: 2);
        var removedMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId, matchNumber: 3);

        var newKickoff = new DateTime(2027, 3, 14, 15, 0, 0, DateTimeKind.Utc);
        var newLockTime = newKickoff.AddHours(-2);

        var edited = new Match(
            id: editedMatchId, roundId: roundId, homeTeamId: backdrop.HomeTeamId, awayTeamId: backdrop.AwayTeamId,
            matchDateTimeUtc: newKickoff, customLockTimeUtc: newLockTime, status: MatchStatus.Postponed,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: 4242, matchNumber: 1,
            placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Id 0, so the diff treats it as an insert.
        var added = Match.Create(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId, newKickoff.AddDays(1), externalId: null);

        var newDeadline = new DateTime(2027, 3, 14, 12, 0, 0, DateTimeKind.Utc);
        var round = new Round(
            id: roundId, seasonId: backdrop.SeasonId, roundNumber: 7, displayName: "Quarter Finals",
            startDateUtc: newDeadline.AddDays(-1), deadlineUtc: newDeadline, status: RoundStatus.InProgress,
            apiRoundName: "Quarter-finals", lastReminderSentUtc: null,
            matches: [edited, ExistingMatch(secondMatchId, roundId, backdrop), added],
            resultsDigestSentUtc: null);

        // Act
        await Repository.UpdateAsync(round, CancellationToken.None);

        // Assert - the removal happened and exactly one fixture was added.
        var remaining = await Inspect.MatchIdsForRoundAsync(roundId);
        remaining.Should().HaveCount(3);
        remaining.Should().Contain(editedMatchId).And.Contain(secondMatchId);
        remaining.Should().NotContain(removedMatchId);

        // Assert - the edit landed on every column the update writes.
        var stored = await Inspect.MatchAsync(editedMatchId);
        stored.Should().NotBeNull();
        stored!.MatchDateTimeUtc.Should().Be(newKickoff);
        stored.CustomLockTimeUtc.Should().Be(newLockTime);
        stored.Status.Should().Be(MatchStatus.Postponed);
        stored.ExternalId.Should().Be(4242);

        // Assert - and the round's own fields, which UpdateAsync writes in the same call.
        var storedRound = await Inspect.RoundAsync(roundId);
        storedRound.Should().NotBeNull();
        storedRound!.RoundNumber.Should().Be(7);
        storedRound.DisplayName.Should().Be("Quarter Finals");
        storedRound.DeadlineUtc.Should().Be(newDeadline);
        storedRound.Status.Should().Be(RoundStatus.InProgress);
        storedRound.ApiRoundName.Should().Be("Quarter-finals");
    }

    #endregion

    #region Storing outcome tallies

    // What the tallies mean, and which predictions count towards them, is Domain.Services.OutcomeTally and is unit
    // tested. What is left for an adapter is the upsert, and the one behaviour worth pinning here is what it does NOT do:
    // a player absent from the batch keeps the row they have. The statement this replaced had no test at all, because it
    // counted and stored in one MERGE and neither half could be reached without a database.

    [Fact]
    public async Task UpdateRoundResultsAsync_ShouldStoreATallyForEachPlayer()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        // Act
        await Repository.UpdateRoundResultsAsync(
            roundId,
            [
                new RoundResultTally(backdrop.UserId, new OutcomeCounts(3, 2, 1)),
                new RoundResultTally(otherUserId, new OutcomeCounts(0, 0, 5))
            ],
            CancellationToken.None);

        // Assert - each count under its own heading, which three SUM(CASE WHEN ...) columns could transpose silently.
        (await Inspect.RoundResultAsync(roundId, backdrop.UserId))
            .Should().Be(new StoredRoundResult(3, 2, 1));

        (await Inspect.RoundResultAsync(roundId, otherUserId))
            .Should().Be(new StoredRoundResult(0, 0, 5));
    }

    [Fact]
    public async Task UpdateRoundResultsAsync_ShouldReplaceATallyThatIsAlreadyStored()
    {
        // Arrange - a round processed once, then re-processed after a score correction.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1));

        await Repository.UpdateRoundResultsAsync(
            roundId, [new RoundResultTally(backdrop.UserId, new OutcomeCounts(3, 2, 1))], CancellationToken.None);

        // Act
        await Repository.UpdateRoundResultsAsync(
            roundId, [new RoundResultTally(backdrop.UserId, new OutcomeCounts(1, 1, 4))], CancellationToken.None);

        // Assert - replaced, not added to.
        (await Inspect.RoundResultAsync(roundId, backdrop.UserId))
            .Should().Be(new StoredRoundResult(1, 1, 4));
    }

    [Fact]
    public async Task UpdateRoundResultsAsync_ShouldLeaveAPlayerAbsentFromTheBatchAlone()
    {
        // A round re-processed with a fixture reverted to unplayed must not wipe everybody else's results. The old MERGE
        // had no WHEN NOT MATCHED BY SOURCE clause, and that omission was load-bearing.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Repository.UpdateRoundResultsAsync(
            roundId,
            [
                new RoundResultTally(backdrop.UserId, new OutcomeCounts(3, 2, 1)),
                new RoundResultTally(otherUserId, new OutcomeCounts(1, 1, 1))
            ],
            CancellationToken.None);

        // Act - only one of them this time.
        await Repository.UpdateRoundResultsAsync(
            roundId, [new RoundResultTally(backdrop.UserId, new OutcomeCounts(4, 0, 0))], CancellationToken.None);

        // Assert
        (await Inspect.RoundResultAsync(roundId, backdrop.UserId)).Should().Be(new StoredRoundResult(4, 0, 0));
        (await Inspect.RoundResultAsync(roundId, otherUserId)).Should().Be(new StoredRoundResult(1, 1, 1));
    }

    [Fact]
    public async Task UpdateRoundResultsAsync_ShouldStoreNothing_WhenThereAreNoTallies()
    {
        // Arrange - a round nobody predicted. Dapper throws on an empty parameter list, so this has to be handled rather
        // than sent.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1));

        // Act
        var act = async () => await Repository.UpdateRoundResultsAsync(roundId, [], CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await Inspect.RoundResultAsync(roundId, backdrop.UserId)).Should().BeNull();
    }

    #endregion

    #region Helpers

    private static Round RoundWith(int roundId, int seasonId, IEnumerable<Match> matches) =>
        new(
            id: roundId, seasonId: seasonId, roundNumber: 1, displayName: "Round 1",
            startDateUtc: DateTime.UtcNow.AddDays(1), deadlineUtc: DateTime.UtcNow.AddDays(2),
            status: RoundStatus.Published, apiRoundName: null, lastReminderSentUtc: null,
            matches: matches, resultsDigestSentUtc: null);

    private static Match ExistingMatch(int matchId, int roundId, SeededBackdrop backdrop) =>
        new(
            id: matchId, roundId: roundId, homeTeamId: backdrop.HomeTeamId, awayTeamId: backdrop.AwayTeamId,
            matchDateTimeUtc: DateTime.UtcNow.AddDays(3), customLockTimeUtc: null, status: MatchStatus.Scheduled,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: null, matchNumber: null,
            placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

    #endregion
}
