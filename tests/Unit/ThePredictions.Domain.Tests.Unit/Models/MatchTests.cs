using FluentAssertions;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class MatchTests
{
    private static readonly DateTime ValidMatchTime = new(2025, 8, 16, 15, 0, 0, DateTimeKind.Utc);

    private static Match CreateMatchViaFactory(
        int roundId = 1,
        int homeTeamId = 1,
        int awayTeamId = 2,
        DateTime? matchDateTimeUtc = null,
        int? externalId = null)
    {
        return Match.Create(roundId, homeTeamId, awayTeamId, matchDateTimeUtc ?? ValidMatchTime, externalId);
    }

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateMatch_WhenValidParametersProvided()
    {
        // Act
        var match = CreateMatchViaFactory();

        // Assert
        match.RoundId.Should().Be(1);
        match.HomeTeamId.Should().Be(1);
        match.AwayTeamId.Should().Be(2);
        match.MatchDateTimeUtc.Should().Be(ValidMatchTime);
        match.Status.Should().Be(MatchStatus.Scheduled);
        match.ActualHomeTeamScore.Should().BeNull();
        match.ActualAwayTeamScore.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetStatusToScheduled_WhenCreated()
    {
        // Act
        var match = CreateMatchViaFactory();

        // Assert
        match.Status.Should().Be(MatchStatus.Scheduled);
    }

    [Fact]
    public void Create_ShouldSetScoresToNull_WhenCreated()
    {
        // Act
        var match = CreateMatchViaFactory();

        // Assert
        match.ActualHomeTeamScore.Should().BeNull();
        match.ActualAwayTeamScore.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetCustomLockTimeUtcToNull_WhenCreated()
    {
        // Act
        var match = CreateMatchViaFactory();

        // Assert
        match.CustomLockTimeUtc.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetPlaceholderNamesToNull_WhenCreated()
    {
        // Act
        var match = CreateMatchViaFactory();

        // Assert
        match.PlaceholderHomeName.Should().BeNull();
        match.PlaceholderAwayName.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldAcceptNullExternalId()
    {
        // Act
        var act = () => CreateMatchViaFactory(externalId: null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_ShouldSetExternalId_WhenProvided()
    {
        // Act
        var match = CreateMatchViaFactory(externalId: 12345);

        // Assert
        match.ExternalId.Should().Be(12345);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenRoundIdIsZero()
    {
        // Act
        var act = () => CreateMatchViaFactory(roundId: 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRoundIdIsNegative()
    {
        // Act
        var act = () => CreateMatchViaFactory(roundId: -1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenTeamPlaysItself()
    {
        // Act
        var act = () => CreateMatchViaFactory(homeTeamId: 5, awayTeamId: 5);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenTeamPlaysItselfWithZeroIds()
    {
        // Act
        var act = () => CreateMatchViaFactory(homeTeamId: 0, awayTeamId: 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenMatchDateIsDefault()
    {
        // Act
        var act = () => CreateMatchViaFactory(matchDateTimeUtc: default(DateTime));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateScore

    [Fact]
    public void UpdateScore_ShouldSetScores_WhenStatusIsCompleted()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.UpdateScore(2, 1, MatchStatus.Completed);

        // Assert
        match.ActualHomeTeamScore.Should().Be(2);
        match.ActualAwayTeamScore.Should().Be(1);
        match.Status.Should().Be(MatchStatus.Completed);
    }

    [Fact]
    public void UpdateScore_ShouldSetScores_WhenStatusIsInProgress()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.UpdateScore(1, 0, MatchStatus.InProgress);

        // Assert
        match.ActualHomeTeamScore.Should().Be(1);
        match.ActualAwayTeamScore.Should().Be(0);
        match.Status.Should().Be(MatchStatus.InProgress);
    }

    [Fact]
    public void UpdateScore_ShouldClearScores_WhenStatusIsScheduled()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.UpdateScore(2, 1, MatchStatus.Completed);

        // Act
        match.UpdateScore(2, 1, MatchStatus.Scheduled);

        // Assert
        match.ActualHomeTeamScore.Should().BeNull();
        match.ActualAwayTeamScore.Should().BeNull();
        match.Status.Should().Be(MatchStatus.Scheduled);
    }

    [Fact]
    public void UpdateScore_ShouldIgnoreScoreValues_WhenStatusIsScheduled()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.UpdateScore(5, 3, MatchStatus.Scheduled);

        // Assert
        match.ActualHomeTeamScore.Should().BeNull();
        match.ActualAwayTeamScore.Should().BeNull();
    }

    [Fact]
    public void UpdateScore_ShouldThrowException_WhenHomeScoreIsNegative()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        var act = () => match.UpdateScore(-1, 0, MatchStatus.Completed);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateScore_ShouldThrowException_WhenAwayScoreIsNegative()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        var act = () => match.UpdateScore(0, -1, MatchStatus.Completed);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateScore_ShouldAllowZeroScores_WhenStatusIsCompleted()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.UpdateScore(0, 0, MatchStatus.Completed);

        // Assert
        match.ActualHomeTeamScore.Should().Be(0);
        match.ActualAwayTeamScore.Should().Be(0);
    }

    [Fact]
    public void UpdateScore_ShouldAllowHighScores_WhenStatusIsCompleted()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.UpdateScore(9, 0, MatchStatus.Completed);

        // Assert
        match.ActualHomeTeamScore.Should().Be(9);
        match.ActualAwayTeamScore.Should().Be(0);
    }

    [Fact]
    public void UpdateScore_ShouldUpdateStatus_WhenCalled()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.UpdateScore(1, 1, MatchStatus.InProgress);

        // Assert
        match.Status.Should().Be(MatchStatus.InProgress);
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateProperties_WhenValid()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        var newDate = ValidMatchTime.AddDays(7);

        // Act
        match.UpdateDetails(3, 4, newDate);

        // Assert
        match.HomeTeamId.Should().Be(3);
        match.AwayTeamId.Should().Be(4);
        match.MatchDateTimeUtc.Should().Be(newDate);
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenTeamPlaysItself()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        var act = () => match.UpdateDetails(5, 5, ValidMatchTime);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenMatchDateIsDefault()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        var act = () => match.UpdateDetails(1, 2, default);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeStatus_WhenUpdating()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.UpdateScore(1, 0, MatchStatus.InProgress);

        // Act
        match.UpdateDetails(3, 4, ValidMatchTime.AddDays(1));

        // Assert
        match.Status.Should().Be(MatchStatus.InProgress);
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeScores_WhenUpdating()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.UpdateScore(2, 1, MatchStatus.Completed);

        // Act
        match.UpdateDetails(3, 4, ValidMatchTime.AddDays(1));

        // Assert
        match.ActualHomeTeamScore.Should().Be(2);
        match.ActualAwayTeamScore.Should().Be(1);
    }

    #endregion

    #region UpdateDate

    [Fact]
    public void UpdateDate_ShouldUpdateMatchDateTime_WhenCalled()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        var newDate = ValidMatchTime.AddDays(14);

        // Act
        match.UpdateDate(newDate);

        // Assert
        match.MatchDateTimeUtc.Should().Be(newDate);
    }

    [Fact]
    public void UpdateDate_ShouldAcceptAnyDateTime_WhenCalled()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act — no validation, accepts even default
        var act = () => match.UpdateDate(default);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region MoveToRound

    [Fact]
    public void MoveToRound_ShouldUpdateRoundId_WhenValid()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.MoveToRound(5);

        // Assert
        match.RoundId.Should().Be(5);
    }

    [Fact]
    public void MoveToRound_ShouldThrowException_WhenRoundIdIsZero()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        var act = () => match.MoveToRound(0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MoveToRound_ShouldThrowException_WhenRoundIdIsNegative()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        var act = () => match.MoveToRound(-1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
