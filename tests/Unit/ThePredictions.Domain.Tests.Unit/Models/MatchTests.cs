using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

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

    [Fact]
    public void UpdateScore_ShouldClearScores_WhenStatusIsPostponed()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.UpdateScore(2, 1, MatchStatus.Completed);

        // Act
        match.UpdateScore(2, 1, MatchStatus.Postponed);

        // Assert
        match.ActualHomeTeamScore.Should().BeNull();
        match.ActualAwayTeamScore.Should().BeNull();
        match.Status.Should().Be(MatchStatus.Postponed);
    }

    [Fact]
    public void UpdateScore_ShouldIgnoreScoreValues_WhenStatusIsPostponed()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.UpdateScore(5, 3, MatchStatus.Postponed);

        // Assert
        match.ActualHomeTeamScore.Should().BeNull();
        match.ActualAwayTeamScore.Should().BeNull();
    }

    #endregion

    #region Postpone

    [Fact]
    public void Postpone_ShouldSetStatusToPostponed_WhenCalled()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.Postpone();

        // Assert
        match.Status.Should().Be(MatchStatus.Postponed);
    }

    [Fact]
    public void Postpone_ShouldClearScores_WhenMatchHadScores()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.UpdateScore(2, 1, MatchStatus.InProgress);

        // Act
        match.Postpone();

        // Assert
        match.ActualHomeTeamScore.Should().BeNull();
        match.ActualAwayTeamScore.Should().BeNull();
    }

    [Fact]
    public void Postpone_ShouldNotThrow_WhenAlreadyPostponed()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.Postpone();

        // Act
        var act = () => match.Postpone();

        // Assert
        act.Should().NotThrow();
        match.Status.Should().Be(MatchStatus.Postponed);
    }

    #endregion

    #region Reschedule

    [Fact]
    public void Reschedule_ShouldSetStatusToScheduled_WhenPostponed()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.Postpone();

        // Act
        match.Reschedule();

        // Assert
        match.Status.Should().Be(MatchStatus.Scheduled);
    }

    [Fact]
    public void Reschedule_ShouldNotChangeStatus_WhenNotPostponed()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.UpdateScore(1, 0, MatchStatus.InProgress);

        // Act
        match.Reschedule();

        // Assert
        match.Status.Should().Be(MatchStatus.InProgress);
    }

    [Fact]
    public void Reschedule_ShouldNotChangeStatus_WhenScheduled()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.Reschedule();

        // Assert
        match.Status.Should().Be(MatchStatus.Scheduled);
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

    #region IsPostponed

    [Theory]
    [InlineData(MatchStatus.Scheduled, false)]
    [InlineData(MatchStatus.InProgress, false)]
    [InlineData(MatchStatus.Completed, false)]
    [InlineData(MatchStatus.Postponed, true)]
    public void IsPostponed_ShouldReflectTheStatus(MatchStatus status, bool expected)
    {
        // Arrange - the three false cases are the whitelist two queries spelled out as
        // "Status IN (@Scheduled, @InProgress, @Completed)".
        var match = new Match(id: 1, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: status, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Act
        var result = match.IsPostponed;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled, false)]
    [InlineData(MatchStatus.InProgress, false)]
    [InlineData(MatchStatus.Completed, false)]
    [InlineData(MatchStatus.Postponed, true)]
    public void IsPostponedStatus_ShouldAnswerTheSameQuestionForAStatusOnItsOwn(MatchStatus status, bool expected)
    {
        // The read paths hold rows rather than fixtures, and both forms have to agree - there is one definition of
        // "called off" or there are two.
        Match.IsPostponedStatus(status).Should().Be(expected);
    }

    #endregion

    #region GetEffectiveDeadline

    [Fact]
    public void GetEffectiveDeadline_ShouldReturnCustomLockTime_WhenSet()
    {
        // Arrange
        var customLockTime = new DateTime(2025, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        var roundDeadline = new DateTime(2025, 8, 14, 11, 0, 0, DateTimeKind.Utc);
        var match = new Match(id: 1, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: customLockTime,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Act
        var result = match.GetEffectiveDeadline(roundDeadline);

        // Assert
        result.Should().Be(customLockTime);
    }

    [Fact]
    public void GetEffectiveDeadline_ShouldReturnRoundDeadline_WhenCustomLockTimeIsNull()
    {
        // Arrange
        var roundDeadline = new DateTime(2025, 8, 14, 11, 0, 0, DateTimeKind.Utc);
        var match = new Match(id: 1, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Act
        var result = match.GetEffectiveDeadline(roundDeadline);

        // Assert
        result.Should().Be(roundDeadline);
    }

    #endregion

    #region IsPredictionLocked

    [Fact]
    public void IsPredictionLocked_ShouldReturnTrue_WhenAfterDeadline()
    {
        // Arrange
        var roundDeadline = new DateTime(2025, 8, 14, 11, 0, 0, DateTimeKind.Utc);
        var utcNow = roundDeadline.AddMinutes(1);
        var match = CreateMatchViaFactory();

        // Act
        var result = match.IsPredictionLocked(utcNow, roundDeadline);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPredictionLocked_ShouldReturnFalse_WhenBeforeDeadline()
    {
        // Arrange
        var roundDeadline = new DateTime(2025, 8, 14, 11, 0, 0, DateTimeKind.Utc);
        var utcNow = roundDeadline.AddMinutes(-1);
        var match = CreateMatchViaFactory();

        // Act
        var result = match.IsPredictionLocked(utcNow, roundDeadline);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AreTeamsConfirmed

    [Fact]
    public void AreTeamsConfirmed_ShouldReturnTrue_WhenBothTeamsSet()
    {
        // Arrange
        var match = new Match(id: 1, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Act & Assert
        match.AreTeamsConfirmed.Should().BeTrue();
    }

    [Fact]
    public void AreTeamsConfirmed_ShouldReturnFalse_WhenHomeTeamIsNull()
    {
        // Arrange
        var match = new Match(id: 1, roundId: 1, homeTeamId: null, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: "Winner Group A", placeholderAwayName: null,
            apiRoundName: null);

        // Act & Assert
        match.AreTeamsConfirmed.Should().BeFalse();
    }

    [Fact]
    public void AreTeamsConfirmed_ShouldReturnFalse_WhenAwayTeamIsNull()
    {
        // Arrange
        var match = new Match(id: 1, roundId: 1, homeTeamId: 1, awayTeamId: null,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: "Runner-up Group B",
            apiRoundName: null);

        // Act & Assert
        match.AreTeamsConfirmed.Should().BeFalse();
    }

    #endregion

    #region AssignTeams

    [Fact]
    public void AssignTeams_ShouldSetTeamsAndClearPlaceholders()
    {
        // Arrange
        var match = new Match(id: 1, roundId: 1, homeTeamId: 10, awayTeamId: 20,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: "Winner Group A", placeholderAwayName: "Winner Group B",
            apiRoundName: null);

        // Act
        match.AssignTeams(30, 40);

        // Assert
        match.HomeTeamId.Should().Be(30);
        match.AwayTeamId.Should().Be(40);
        match.PlaceholderHomeName.Should().BeNull();
        match.PlaceholderAwayName.Should().BeNull();
    }

    [Fact]
    public void AssignTeams_ShouldThrow_WhenTeamPlaysItself()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        var act = () => match.AssignTeams(5, 5);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreatePlaceholder

    [Fact]
    public void CreatePlaceholder_ShouldCreateMatch_WhenValidParametersProvided()
    {
        // Act
        var match = Match.CreatePlaceholder(1, "Semi-final 1", "Semi-final 1", "Semi-finals");

        // Assert
        match.RoundId.Should().Be(1);
        match.HomeTeamId.Should().BeNull();
        match.AwayTeamId.Should().BeNull();
        match.PlaceholderHomeName.Should().Be("Semi-final 1");
        match.PlaceholderAwayName.Should().Be("Semi-final 1");
        match.ApiRoundName.Should().Be("Semi-finals");
        match.Status.Should().Be(MatchStatus.Scheduled);
        match.AreTeamsConfirmed.Should().BeFalse();
        match.ExternalId.Should().BeNull();
        match.CustomLockTimeUtc.Should().BeNull();
        match.MatchDateTimeUtc.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void CreatePlaceholder_ShouldThrow_WhenRoundIdIsZero()
    {
        // Act
        var act = () => Match.CreatePlaceholder(0, "TBC", "TBC", "Final");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePlaceholder_ShouldThrow_WhenPlaceholderHomeNameIsEmpty()
    {
        // Act
        var act = () => Match.CreatePlaceholder(1, "", "TBC", "Final");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePlaceholder_ShouldThrow_WhenPlaceholderAwayNameIsEmpty()
    {
        // Act
        var act = () => Match.CreatePlaceholder(1, "TBC", "", "Final");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region SetCustomLockTime

    [Fact]
    public void SetCustomLockTime_ShouldSetValue_WhenProvided()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        var lockTime = new DateTime(2025, 8, 16, 14, 30, 0, DateTimeKind.Utc);

        // Act
        match.SetCustomLockTime(lockTime);

        // Assert
        match.CustomLockTimeUtc.Should().Be(lockTime);
    }

    [Fact]
    public void SetCustomLockTime_ShouldClearValue_WhenNull()
    {
        // Arrange
        var match = CreateMatchViaFactory();
        match.SetCustomLockTime(new DateTime(2025, 8, 16, 14, 30, 0, DateTimeKind.Utc));

        // Act
        match.SetCustomLockTime(null);

        // Assert
        match.CustomLockTimeUtc.Should().BeNull();
    }

    #endregion

    #region SetExternalId

    [Fact]
    public void SetExternalId_ShouldSetValue_WhenProvided()
    {
        // Arrange
        var match = Match.CreatePlaceholder(1, "TBC", "TBC", "Final");

        // Act
        match.SetExternalId(12345);

        // Assert
        match.ExternalId.Should().Be(12345);
    }

    #endregion

    #region SetApiRoundName

    [Fact]
    public void SetApiRoundName_ShouldSetValue_WhenProvided()
    {
        // Arrange
        var match = CreateMatchViaFactory();

        // Act
        match.SetApiRoundName("Quarter-finals");

        // Assert
        match.ApiRoundName.Should().Be("Quarter-finals");
    }

    #endregion
}
