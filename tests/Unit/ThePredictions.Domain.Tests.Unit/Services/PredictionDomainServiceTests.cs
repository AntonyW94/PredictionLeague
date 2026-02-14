using FluentAssertions;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using PredictionLeague.Domain.Services;
using ThePredictions.Domain.Tests.Unit.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

public class PredictionDomainServiceTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    private readonly PredictionDomainService _sut;

    public PredictionDomainServiceTests()
    {
        _sut = new PredictionDomainService(_dateTimeProvider);
    }

    private Round CreateRoundWithFutureDeadline() =>
        new(id: 1, seasonId: 1, roundNumber: 1,
            startDateUtc: _dateTimeProvider.UtcNow.AddDays(2),
            deadlineUtc: _dateTimeProvider.UtcNow.AddDays(1),
            status: RoundStatus.Published,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: null);

    private Round CreateRoundWithPastDeadline() =>
        new(id: 1, seasonId: 1, roundNumber: 1,
            startDateUtc: _dateTimeProvider.UtcNow.AddDays(-1),
            deadlineUtc: _dateTimeProvider.UtcNow.AddDays(-2),
            status: RoundStatus.InProgress,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: null);

    #region SubmitPredictions — Happy Path

    [Fact]
    public void SubmitPredictions_ShouldReturnPredictions_WhenDeadlineNotPassed()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 2, AwayScore: 1) };

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void SubmitPredictions_ShouldCreateCorrectNumberOfPredictions_WhenMultipleScoresProvided()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 2, AwayScore: 1),
            (MatchId: 2, HomeScore: 0, AwayScore: 0),
            (MatchId: 3, HomeScore: 3, AwayScore: 2)
        };

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void SubmitPredictions_ShouldSetUserIdOnAllPredictions_WhenCreated()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 2, AwayScore: 1),
            (MatchId: 2, HomeScore: 0, AwayScore: 0)
        };

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result.Should().AllSatisfy(p => p.UserId.Should().Be("user-1"));
    }

    [Fact]
    public void SubmitPredictions_ShouldSetCorrectMatchIds_WhenCreated()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 10, HomeScore: 1, AwayScore: 0),
            (MatchId: 20, HomeScore: 2, AwayScore: 2),
            (MatchId: 30, HomeScore: 0, AwayScore: 3)
        };

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result.Select(p => p.MatchId).Should().BeEquivalentTo(new[] { 10, 20, 30 });
    }

    [Fact]
    public void SubmitPredictions_ShouldSetCorrectScores_WhenCreated()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = new[]
        {
            (MatchId: 1, HomeScore: 2, AwayScore: 1),
            (MatchId: 2, HomeScore: 0, AwayScore: 3)
        };

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result[0].PredictedHomeScore.Should().Be(2);
        result[0].PredictedAwayScore.Should().Be(1);
        result[1].PredictedHomeScore.Should().Be(0);
        result[1].PredictedAwayScore.Should().Be(3);
    }

    [Fact]
    public void SubmitPredictions_ShouldSetPendingOutcome_WhenCreated()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 2, AwayScore: 1) };

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result.Should().AllSatisfy(p => p.Outcome.Should().Be(PredictionOutcome.Pending));
    }

    [Fact]
    public void SubmitPredictions_ShouldReturnEmptyCollection_WhenEmptyPredictionsListProvided()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = Array.Empty<(int MatchId, int HomeScore, int AwayScore)>();

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SubmitPredictions_ShouldReturnSinglePrediction_WhenOnlyOneScoreProvided()
    {
        // Arrange
        var round = CreateRoundWithFutureDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 1, AwayScore: 1) };

        // Act
        var result = _sut.SubmitPredictions(round, "user-1", scores).ToList();

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region SubmitPredictions — Validation

    [Fact]
    public void SubmitPredictions_ShouldThrowException_WhenDeadlineHasPassed()
    {
        // Arrange
        var round = CreateRoundWithPastDeadline();
        var scores = new[] { (MatchId: 1, HomeScore: 1, AwayScore: 0) };

        // Act
        var act = () => _sut.SubmitPredictions(round, "user-1", scores);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SubmitPredictions_ShouldThrowException_WhenRoundIsNull()
    {
        // Arrange
        var scores = new[] { (MatchId: 1, HomeScore: 1, AwayScore: 0) };

        // Act
        var act = () => _sut.SubmitPredictions(null!, "user-1", scores);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
