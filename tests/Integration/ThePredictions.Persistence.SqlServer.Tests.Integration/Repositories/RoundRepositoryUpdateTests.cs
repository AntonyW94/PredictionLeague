using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Persistence.SqlServer.Repositories;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Repositories;

/// <summary>
/// <c>RoundRepository.UpdateAsync</c> works out which matches to insert, update and delete by diffing the
/// incoming round against the match ids already stored, and the delete is guarded by
/// <c>NOT EXISTS (SELECT 1 FROM [UserPredictions] ...)</c>. That guard is the only thing standing between
/// an ordinary round edit and silent data loss: <c>FK_UserPredictions_Matches</c> is
/// <c>ON DELETE CASCADE</c>, so deleting a match takes every player's prediction for it with no error and
/// no trace. <see cref="UserPredictions_ShouldBeCascadeDeleted_WhenAMatchIsDeletedWithoutTheGuard"/> pins
/// that premise, so the guard's importance is demonstrated rather than asserted in a comment.
///
/// None of this is reachable by a unit test: the rule lives in a SQL predicate, and a mocked connection
/// only proves a string was handed over.
/// </summary>
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class RoundRepositoryUpdateTests(SqlServerDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string SelectMatchIdsForRound = "SELECT [Id] FROM [Matches] WHERE [RoundId] = @RoundId;";

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
        await CreateRepository().UpdateAsync(round, CancellationToken.None);

        // Assert
        var remainingMatchIds = await QueryAsync<int>(SelectMatchIdsForRound, new { RoundId = roundId });
        remainingMatchIds.Should().BeEquivalentTo(new[] { keptMatchId, predictedMatchId },
            "a fixture players have already predicted must survive the edit.");

        var predictionCount = await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [UserPredictions] WHERE [MatchId] = @MatchId;", new { MatchId = predictedMatchId });
        predictionCount.Should().Be(1, "the predictions are what the guard exists to protect.");
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
        await CreateRepository().UpdateAsync(round, CancellationToken.None);

        // Assert - the guard protects predictions, not fixtures, so this one goes.
        var remainingMatchIds = await QueryAsync<int>(SelectMatchIdsForRound, new { RoundId = roundId });
        remainingMatchIds.Should().BeEquivalentTo(new[] { keptMatchId });
        remainingMatchIds.Should().NotContain(unpredictedMatchId);
    }

    [Fact]
    public async Task UserPredictions_ShouldBeCascadeDeleted_WhenAMatchIsDeletedWithoutTheGuard()
    {
        // Arrange - this test asserts the schema, not the repository. It is here because it is the
        // reason the guard exists: without it a removed fixture silently takes its predictions.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var matchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddPredictionAsync(matchId, backdrop.UserId);

        // Act - an unguarded delete, as UpdateAsync would issue if the NOT EXISTS were dropped.
        await ExecuteAsync("DELETE FROM [Matches] WHERE [Id] = @MatchId;", new { MatchId = matchId });

        // Assert
        var predictionCount = await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [UserPredictions] WHERE [MatchId] = @MatchId;", new { MatchId = matchId });
        predictionCount.Should().Be(0,
            "FK_UserPredictions_Matches is ON DELETE CASCADE - the delete succeeds and the predictions "
            + "vanish without an error, which is why the NOT EXISTS guard is load-bearing.");
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

        var repository = CreateRepository();

        // Act
        await repository.MoveMatchesToRoundAsync([movedMatchId], targetRoundId, CancellationToken.None);
        var round = RoundWith(sourceRoundId, backdrop.SeasonId, [ExistingMatch(stayingMatchId, sourceRoundId, backdrop)]);
        await repository.UpdateAsync(round, CancellationToken.None);

        // Assert - the move took the fixture out of the round before the diff was computed, so the diff
        // never saw it as removed.
        var movedMatchRoundId = await ScalarAsync<int>(
            "SELECT [RoundId] FROM [Matches] WHERE [Id] = @MatchId;", new { MatchId = movedMatchId });
        movedMatchRoundId.Should().Be(targetRoundId, "the moved fixture must survive the update of the round it left.");
    }

    [Fact]
    public async Task UpdateAsync_ShouldDeleteAMatchDestinedForAnotherRound_WhenTheMoveRunsAfterwards()
    {
        // Arrange - the same scenario in the wrong order. This is not desirable behaviour; it is the
        // ordering dependency written down, so that a caller reordering the two calls fails here rather
        // than in production.
        var backdrop = await Seed.AddBackdropAsync();
        var sourceRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var targetRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 2, deadlineUtc: DateTime.UtcNow.AddDays(9));
        var stayingMatchId = await Seed.AddMatchAsync(sourceRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var matchToMoveId = await Seed.AddMatchAsync(sourceRoundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        var repository = CreateRepository();

        // Act
        var round = RoundWith(sourceRoundId, backdrop.SeasonId, [ExistingMatch(stayingMatchId, sourceRoundId, backdrop)]);
        await repository.UpdateAsync(round, CancellationToken.None);
        await repository.MoveMatchesToRoundAsync([matchToMoveId], targetRoundId, CancellationToken.None);

        // Assert
        var matchExists = await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [Matches] WHERE [Id] = @MatchId;", new { MatchId = matchToMoveId });
        matchExists.Should().Be(0,
            "UpdateAsync treats a match absent from the incoming round as removed, so MoveMatchesToRoundAsync "
            + "must run before it - see UpdateRoundCommandHandler.");
    }

    #endregion

    #region Insert, update and delete together

    [Fact]
    public async Task UpdateAsync_ShouldInsertUpdateAndDeleteTogether_WhenOneCallDoesAllThree()
    {
        // Arrange - three fixtures: one to be edited, one to be left alone, one to be removed.
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
        await CreateRepository().UpdateAsync(round, CancellationToken.None);

        // Assert - the removal happened and exactly one fixture was added.
        var remainingMatchIds = await QueryAsync<int>(SelectMatchIdsForRound, new { RoundId = roundId });
        remainingMatchIds.Should().HaveCount(3);
        remainingMatchIds.Should().Contain(editedMatchId).And.Contain(secondMatchId);
        remainingMatchIds.Should().NotContain(removedMatchId);

        // Assert - the edit landed on every column the update writes.
        var stored = (await QueryAsync<MatchRow>(
            @"
            SELECT
                m.[MatchDateTimeUtc],
                m.[CustomLockTimeUtc],
                m.[Status],
                m.[ExternalId]
            FROM
                [Matches] m
            WHERE
                m.[Id] = @MatchId;",
            new { MatchId = editedMatchId })).Single();

        stored.MatchDateTimeUtc.Should().Be(newKickoff);
        stored.CustomLockTimeUtc.Should().Be(newLockTime);
        stored.Status.Should().Be(nameof(MatchStatus.Postponed));
        stored.ExternalId.Should().Be(4242);

        // Assert - and the round's own fields, which UpdateAsync writes in the same call.
        var storedRound = (await QueryAsync<RoundRow>(
            @"
            SELECT
                r.[RoundNumber],
                r.[DisplayName],
                r.[DeadlineUtc],
                r.[Status],
                r.[ApiRoundName]
            FROM
                [Rounds] r
            WHERE
                r.[Id] = @RoundId;",
            new { RoundId = roundId })).Single();

        storedRound.RoundNumber.Should().Be(7);
        storedRound.DisplayName.Should().Be("Quarter Finals");
        storedRound.DeadlineUtc.Should().Be(newDeadline);
        storedRound.Status.Should().Be(nameof(RoundStatus.InProgress));
        storedRound.ApiRoundName.Should().Be("Quarter-finals");
    }

    [Fact]
    public async Task UpdateAsync_ShouldDeleteOnlyTheUnpredictedMatches_WhenSeveralAreRemovedAtOnce()
    {
        // Arrange - two removals in one call, one of them predicted. The delete runs as a single
        // statement over both ids, so the guard has to discriminate row by row.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber: 1, deadlineUtc: DateTime.UtcNow.AddDays(2));
        var keptMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var predictedMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);
        var unpredictedMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddPredictionAsync(predictedMatchId, backdrop.UserId);

        // Act
        var round = RoundWith(roundId, backdrop.SeasonId, [ExistingMatch(keptMatchId, roundId, backdrop)]);
        await CreateRepository().UpdateAsync(round, CancellationToken.None);

        // Assert
        var remainingMatchIds = await QueryAsync<int>(SelectMatchIdsForRound, new { RoundId = roundId });
        remainingMatchIds.Should().BeEquivalentTo(new[] { keptMatchId, predictedMatchId });
        remainingMatchIds.Should().NotContain(unpredictedMatchId);
    }

    #endregion

    #region Helpers

    private RoundRepository CreateRepository() => new(ConnectionFactory, NewTransactionContext());

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

    private sealed record MatchRow(DateTime MatchDateTimeUtc, DateTime? CustomLockTimeUtc, string Status, int? ExternalId);

    private sealed record RoundRow(int RoundNumber, string DisplayName, DateTime DeadlineUtc, string Status, string? ApiRoundName);

    #endregion
}
