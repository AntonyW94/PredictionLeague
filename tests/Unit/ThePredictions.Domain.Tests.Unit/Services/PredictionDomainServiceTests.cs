using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Tests.Unit.Services;

public class PredictionDomainServiceTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    private readonly PredictionDomainService _sut;

    public PredictionDomainServiceTests()
    {
        _sut = new PredictionDomainService(_dateTimeProvider);
    }

    private static Match CreateConfirmedMatch(int id, int roundId, DateTime? customLockTimeUtc = null) =>
        new(id: id, roundId: roundId, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: new DateTime(2025, 6, 20, 15, 0, 0, DateTimeKind.Utc),
            customLockTimeUtc: customLockTimeUtc,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

    private static Match CreatePlaceholderMatch(int id, int roundId) =>
        new(id: id, roundId: roundId, homeTeamId: null, awayTeamId: null,
            matchDateTimeUtc: DateTime.MaxValue,
            customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: "TBC", placeholderAwayName: "TBC", apiRoundName: null);

    private Round CreateRoundWithFutureDeadline(List<Match>? matches = null) =>
        new(id: 1, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: _dateTimeProvider.UtcNow.AddDays(2),
            deadlineUtc: _dateTimeProvider.UtcNow.AddDays(1),
            status: RoundStatus.Published,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: matches ?? new List<Match>
            {
                CreateConfirmedMatch(1, 1),
                CreateConfirmedMatch(2, 1),
                CreateConfirmedMatch(3, 1)
            });

    private Round CreateRoundWithPastDeadline() =>
        new(id: 1, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: _dateTimeProvider.UtcNow.AddDays(-1),
            deadlineUtc: _dateTimeProvider.UtcNow.AddDays(-2),
            status: RoundStatus.InProgress,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: new List<Match> { CreateConfirmedMatch(1, 1) });

    /// <summary>A fixture that has been called off, and whose lock time has not yet passed.</summary>
    private Match CreatePostponedMatch(int id, int roundId) =>
        new(id: id, roundId: roundId, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: _dateTimeProvider.UtcNow.AddDays(5),
            customLockTimeUtc: _dateTimeProvider.UtcNow.AddDays(4),
            status: MatchStatus.Postponed, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

    #region SubmitPredictions - a fixture that has been called off

    [Fact]
    public void SubmitPredictions_ShouldNotAcceptAPredictionForAPostponedMatch()
    {
        // The filter was "teams confirmed and not yet locked", which left out "still scheduled" - so a called-off fixture
        // whose lock time had not passed could still take a prediction. The prediction page does not offer postponed
        // fixtures, but the endpoint takes match ids from the caller.
        var round = CreateRoundWithFutureDeadline([CreateConfirmedMatch(1, 1), CreatePostponedMatch(2, 1)]);

        // Act
        var predictions = _sut.SubmitPredictions(round, "user-1", [(1, 2, 1), (2, 3, 0)]);

        // Assert
        predictions.Select(prediction => prediction.MatchId).Should().Equal(1);
    }

    #endregion

    #region SubmitPredictions — Happy Path

    [Fact]
    public void SubmitPredictions_ShouldReturnPredictions_WhenDeadlineNotPassed()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 2, AwayScore: 1) };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public void SubmitPredictions_ShouldCreateCorrectNumberOfPredictions_WhenMultipleScoresProvided()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 2, AwayScore: 1),
            (MatchId: 2, HomeScore: 0, AwayScore: 0),
            (MatchId: 3, HomeScore: 3, AwayScore: 2)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(3);
    }

    [Fact]
    public void SubmitPredictions_ShouldSetUserIdOnAllPredictions_WhenCreated()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 2, AwayScore: 1),
            (MatchId: 2, HomeScore: 0, AwayScore: 0)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().AllSatisfy(p => p.UserId.Should().Be("user-1"));
    }

    [Fact]
    public void SubmitPredictions_ShouldSetCorrectMatchIds_WhenCreated()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 2, HomeScore: 2, AwayScore: 2),
            (MatchId: 3, HomeScore: 0, AwayScore: 3)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Select(p => p.MatchId).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void SubmitPredictions_ShouldSetCorrectScores_WhenCreated()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 2, AwayScore: 1),
            (MatchId: 2, HomeScore: 0, AwayScore: 3)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result[0].PredictedHomeScore.Should().Be(2);
        result[0].PredictedAwayScore.Should().Be(1);
        result[1].PredictedHomeScore.Should().Be(0);
        result[1].PredictedAwayScore.Should().Be(3);
    }

    [Fact]
    public void SubmitPredictions_ShouldSetPendingOutcome_WhenCreated()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 2, AwayScore: 1) };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().AllSatisfy(p => p.Outcome.Should().Be(PredictionOutcome.Pending));
    }

    [Fact]
    public void SubmitPredictions_ShouldReturnEmptyCollection_WhenEmptyPredictionsListProvided()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = Array.Empty<(int MatchId, int HomeScore, int AwayScore)>();

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void SubmitPredictions_ShouldReturnSinglePrediction_WhenOnlyOneScoreProvided()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 1, AwayScore: 1) };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(1);
    }

    #endregion

    #region SubmitPredictions — Validation

    [Fact]
    public void SubmitPredictions_ShouldThrowException_WhenDeadlineHasPassed()
    {
        var round = CreateRoundWithPastDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 1, AwayScore: 0) };

        var act = () => _sut.SubmitPredictions(round, "user-1", scores);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void SubmitPredictions_ShouldNotThrowAndAcceptOpenMatch_WhenRoundDeadlinePassedButMatchStillOpenViaCustomLock()
    {
        // A combined round (e.g. semi-finals + final): the round deadline has passed and locked the
        // semi-final, but the final carries a later custom lock and is still open. Submission must be
        // accepted for the open match and skip the locked one, rather than being rejected outright.
        var futureLockTime = _dateTimeProvider.UtcNow.AddDays(3);
        var matches = new List<Match>
        {
            CreateConfirmedMatch(1, 1),
            CreateConfirmedMatch(2, 1, customLockTimeUtc: futureLockTime)
        };
        var round = new Round(
            id: 1, seasonId: 1, roundNumber: 1, displayName: "Finals",
            startDateUtc: _dateTimeProvider.UtcNow.AddDays(-1),
            deadlineUtc: _dateTimeProvider.UtcNow.AddDays(-2),
            status: RoundStatus.InProgress,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: matches);
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 2, HomeScore: 2, AwayScore: 1)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().ContainSingle();
        result[0].MatchId.Should().Be(2);
    }

    [Fact]
    public void SubmitPredictions_ShouldThrowException_WhenRoundIsNull()
    {
        var scores = new[] { (MatchId: 1, HomeScore: 1, AwayScore: 0) };

        var act = () => _sut.SubmitPredictions(null!, "user-1", scores);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region SubmitPredictions — TBC Match Filtering

    [Fact]
    public void SubmitPredictions_ShouldSkipPrediction_WhenMatchTeamsNotConfirmed()
    {
        var matches = new List<Match>
        {
            CreateConfirmedMatch(1, 1),
            CreatePlaceholderMatch(2, 1)
        };
        var round = CreateRoundWithFutureDeadline(matches);
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 2, HomeScore: 2, AwayScore: 1)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(1);
        result[0].MatchId.Should().Be(1);
    }

    [Fact]
    public void SubmitPredictions_ShouldReturnEmpty_WhenAllMatchesAreTbc()
    {
        var matches = new List<Match>
        {
            CreatePlaceholderMatch(1, 1),
            CreatePlaceholderMatch(2, 1)
        };
        var round = CreateRoundWithFutureDeadline(matches);
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 2, HomeScore: 2, AwayScore: 1)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void SubmitPredictions_ShouldSkipPrediction_WhenMatchNotInRound()
    {
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 999, HomeScore: 2, AwayScore: 1)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(1);
        result[0].MatchId.Should().Be(1);
    }

    #endregion

    #region SubmitPredictions — Per-Match Custom Deadline

    [Fact]
    public void SubmitPredictions_ShouldSkipPrediction_WhenMatchCustomLockTimePassed()
    {
        var pastLockTime = _dateTimeProvider.UtcNow.AddHours(-1);
        var matches = new List<Match>
        {
            CreateConfirmedMatch(1, 1),
            CreateConfirmedMatch(2, 1, customLockTimeUtc: pastLockTime)
        };
        var round = CreateRoundWithFutureDeadline(matches);
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 2, HomeScore: 2, AwayScore: 1)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(1);
        result[0].MatchId.Should().Be(1);
    }

    [Fact]
    public void SubmitPredictions_ShouldAcceptPrediction_WhenMatchCustomLockTimeNotPassed()
    {
        var futureLockTime = _dateTimeProvider.UtcNow.AddHours(1);
        var matches = new List<Match>
        {
            CreateConfirmedMatch(1, 1, customLockTimeUtc: futureLockTime),
            CreateConfirmedMatch(2, 1, customLockTimeUtc: futureLockTime)
        };
        var round = CreateRoundWithFutureDeadline(matches);
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 2, HomeScore: 2, AwayScore: 1)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void SubmitPredictions_ShouldReturnOnlyOpenMatches_WhenMixOfLockedTbcAndOpen()
    {
        var pastLockTime = _dateTimeProvider.UtcNow.AddHours(-1);
        var matches = new List<Match>
        {
            CreateConfirmedMatch(1, 1),
            CreatePlaceholderMatch(2, 1),
            CreateConfirmedMatch(3, 1, customLockTimeUtc: pastLockTime),
            CreateConfirmedMatch(4, 1)
        };
        var round = CreateRoundWithFutureDeadline(matches);
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 1, AwayScore: 0),
            (MatchId: 2, HomeScore: 2, AwayScore: 1),
            (MatchId: 3, HomeScore: 0, AwayScore: 0),
            (MatchId: 4, HomeScore: 3, AwayScore: 2),
            (MatchId: 999, HomeScore: 1, AwayScore: 1)
        };

        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        result.Should().HaveCount(2);
        result.Select(p => p.MatchId).Should().BeEquivalentTo([1, 4]);
    }

    #endregion
}
