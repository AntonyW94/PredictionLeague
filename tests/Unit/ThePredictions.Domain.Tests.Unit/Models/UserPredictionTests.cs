using FluentAssertions;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using ThePredictions.Domain.Tests.Unit.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class UserPredictionTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreatePrediction_WhenValidParametersProvided()
    {
        // Act
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Assert
        prediction.UserId.Should().Be("user-1");
        prediction.MatchId.Should().Be(1);
        prediction.PredictedHomeScore.Should().Be(2);
        prediction.PredictedAwayScore.Should().Be(1);
        prediction.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public void Create_ShouldSetCreatedAtUtcAndUpdatedAtUtc_WhenCreated()
    {
        // Act
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Assert
        prediction.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        prediction.UpdatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldAllowZeroScores_WhenBothScoresAreZero()
    {
        // Act
        var prediction = UserPrediction.Create("user-1", 1, 0, 0, _dateTimeProvider);

        // Assert
        prediction.PredictedHomeScore.Should().Be(0);
        prediction.PredictedAwayScore.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldAllowHighScores_WhenLargeScoresProvided()
    {
        // Act
        var prediction = UserPrediction.Create("user-1", 1, 9, 0, _dateTimeProvider);

        // Assert
        prediction.PredictedHomeScore.Should().Be(9);
        prediction.PredictedAwayScore.Should().Be(0);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsNull()
    {
        // Act
        var act = () => UserPrediction.Create(null!, 1, 2, 1, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => UserPrediction.Create("", 1, 2, 1, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Act
        var act = () => UserPrediction.Create(" ", 1, 2, 1, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenMatchIdIsZero()
    {
        // Act
        var act = () => UserPrediction.Create("user-1", 0, 2, 1, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenMatchIdIsNegative()
    {
        // Act
        var act = () => UserPrediction.Create("user-1", -1, 2, 1, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenHomeScoreIsNegative()
    {
        // Act
        var act = () => UserPrediction.Create("user-1", 1, -1, 1, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAwayScoreIsNegative()
    {
        // Act
        var act = () => UserPrediction.Create("user-1", 1, 2, -1, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region SetOutcome — Pending Scenarios

    [Fact]
    public void SetOutcome_ShouldReturnPending_WhenStatusIsScheduled()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Scheduled, 2, 1, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public void SetOutcome_ShouldReturnPending_WhenHomeScoreIsNull()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, null, 1, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public void SetOutcome_ShouldReturnPending_WhenAwayScoreIsNull()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, null, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    [Fact]
    public void SetOutcome_ShouldReturnPending_WhenBothScoresAreNull()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, null, null, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Pending);
    }

    #endregion

    #region SetOutcome — ExactScore Scenarios

    [Fact]
    public void SetOutcome_ShouldReturnExactScore_WhenPredictionMatchesExactly()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, 1, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    [Fact]
    public void SetOutcome_ShouldReturnExactScore_WhenBothTeamsScoreZero()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 0, 0, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 0, 0, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    [Fact]
    public void SetOutcome_ShouldReturnExactScore_WhenHighScoringDraw()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 3, 3, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 3, 3, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    [Fact]
    public void SetOutcome_ShouldReturnExactScore_WhenAwayWinMatchesExactly()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 0, 2, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 0, 2, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    [Fact]
    public void SetOutcome_ShouldReturnExactScore_WhenHighScoringMatchMatchesExactly()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 5, 4, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 5, 4, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    #endregion

    #region SetOutcome — CorrectResult Scenarios

    [Fact]
    public void SetOutcome_ShouldReturnCorrectResult_WhenHomeWinPredictedCorrectly()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 3, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, 0, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.CorrectResult);
    }

    [Fact]
    public void SetOutcome_ShouldReturnCorrectResult_WhenAwayWinPredictedCorrectly()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 0, 2, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 1, 3, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.CorrectResult);
    }

    [Fact]
    public void SetOutcome_ShouldReturnCorrectResult_WhenDrawPredictedCorrectly()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 1, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 0, 0, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.CorrectResult);
    }

    [Fact]
    public void SetOutcome_ShouldReturnCorrectResult_WhenDrawWithDifferentScores()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 0, 0, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, 2, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.CorrectResult);
    }

    #endregion

    #region SetOutcome — Incorrect Scenarios

    [Fact]
    public void SetOutcome_ShouldReturnIncorrect_WhenHomeWinPredictedButAwayWon()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 0, 3, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Incorrect);
    }

    [Fact]
    public void SetOutcome_ShouldReturnIncorrect_WhenHomeWinPredictedButDraw()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 1, 1, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Incorrect);
    }

    [Fact]
    public void SetOutcome_ShouldReturnIncorrect_WhenDrawPredictedButHomeWon()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 1, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, 0, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Incorrect);
    }

    [Fact]
    public void SetOutcome_ShouldReturnIncorrect_WhenDrawPredictedButAwayWon()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 1, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 0, 2, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Incorrect);
    }

    [Fact]
    public void SetOutcome_ShouldReturnIncorrect_WhenAwayWinPredictedButHomeWon()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 0, 2, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 3, 0, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Incorrect);
    }

    [Fact]
    public void SetOutcome_ShouldReturnIncorrect_WhenAwayWinPredictedButDraw()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 0, 2, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 1, 1, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.Incorrect);
    }

    #endregion

    #region SetOutcome — State Update

    [Fact]
    public void SetOutcome_ShouldUpdateUpdatedAtUtc_WhenCalled()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);
        var laterTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProvider.UtcNow = laterTime;

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, 1, _dateTimeProvider);

        // Assert
        prediction.UpdatedAtUtc.Should().Be(laterTime);
    }

    #endregion
}
