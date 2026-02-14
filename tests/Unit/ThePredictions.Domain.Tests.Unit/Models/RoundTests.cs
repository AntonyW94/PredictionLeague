using FluentAssertions;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using ThePredictions.Domain.Tests.Unit.Helpers;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class RoundTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    private static readonly DateTime ValidStartDate = new(2025, 8, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ValidDeadline = new(2025, 8, 16, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ValidMatchTime = new(2025, 8, 16, 15, 0, 0, DateTimeKind.Utc);

    private static Round CreateRoundViaFactory(
        int seasonId = 1,
        int roundNumber = 1,
        DateTime? startDateUtc = null,
        DateTime? deadlineUtc = null,
        string? apiRoundName = null)
    {
        return Round.Create(
            seasonId,
            roundNumber,
            startDateUtc ?? ValidStartDate,
            deadlineUtc ?? ValidDeadline,
            apiRoundName);
    }

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateRound_WhenValidParametersProvided()
    {
        // Act
        var round = CreateRoundViaFactory();

        // Assert
        round.SeasonId.Should().Be(1);
        round.RoundNumber.Should().Be(1);
        round.StartDateUtc.Should().Be(ValidStartDate);
        round.DeadlineUtc.Should().Be(ValidDeadline);
        round.Status.Should().Be(RoundStatus.Draft);
    }

    [Fact]
    public void Create_ShouldSetStatusToDraft_WhenCreated()
    {
        // Act
        var round = CreateRoundViaFactory();

        // Assert
        round.Status.Should().Be(RoundStatus.Draft);
    }

    [Fact]
    public void Create_ShouldSetLastReminderSentUtcToNull_WhenCreated()
    {
        // Act
        var round = CreateRoundViaFactory();

        // Assert
        round.LastReminderSentUtc.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetCompletedDateUtcToNull_WhenCreated()
    {
        // Act
        var round = CreateRoundViaFactory();

        // Assert
        round.CompletedDateUtc.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldInitialiseEmptyMatchesCollection_WhenCreated()
    {
        // Act
        var round = CreateRoundViaFactory();

        // Assert
        round.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldAcceptNullApiRoundName()
    {
        // Act
        var act = () => CreateRoundViaFactory(apiRoundName: null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_ShouldSetApiRoundName_WhenProvided()
    {
        // Act
        var round = CreateRoundViaFactory(apiRoundName: "GW1");

        // Assert
        round.ApiRoundName.Should().Be("GW1");
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenSeasonIdIsZero()
    {
        // Act
        var act = () => CreateRoundViaFactory(seasonId: 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenSeasonIdIsNegative()
    {
        // Act
        var act = () => CreateRoundViaFactory(seasonId: -1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRoundNumberIsZero()
    {
        // Act
        var act = () => CreateRoundViaFactory(roundNumber: 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRoundNumberIsNegative()
    {
        // Act
        var act = () => CreateRoundViaFactory(roundNumber: -1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenStartDateIsDefault()
    {
        // Act
        var act = () => CreateRoundViaFactory(startDateUtc: default);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenDeadlineIsDefault()
    {
        // Act
        var act = () => CreateRoundViaFactory(deadlineUtc: default);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenDeadlineIsAfterStartDate()
    {
        // Act
        var act = () => CreateRoundViaFactory(
            startDateUtc: ValidStartDate,
            deadlineUtc: ValidStartDate.AddHours(1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenDeadlineEqualsStartDate()
    {
        // Act
        var act = () => CreateRoundViaFactory(
            startDateUtc: ValidStartDate,
            deadlineUtc: ValidStartDate);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateStatus

    [Fact]
    public void UpdateStatus_ShouldSetCompletedDate_WhenTransitioningFromDraftToCompleted()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);

        // Assert
        round.CompletedDateUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void UpdateStatus_ShouldSetCompletedDate_WhenTransitioningFromPublishedToCompleted()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.UpdateStatus(RoundStatus.Published, _dateTimeProvider);

        // Act
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);

        // Assert
        round.CompletedDateUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void UpdateStatus_ShouldSetCompletedDate_WhenTransitioningFromInProgressToCompleted()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.UpdateStatus(RoundStatus.InProgress, _dateTimeProvider);

        // Act
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);

        // Assert
        round.CompletedDateUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void UpdateStatus_ShouldClearCompletedDate_WhenTransitioningFromCompletedToDraft()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);

        // Act
        round.UpdateStatus(RoundStatus.Draft, _dateTimeProvider);

        // Assert
        round.CompletedDateUtc.Should().BeNull();
    }

    [Fact]
    public void UpdateStatus_ShouldClearCompletedDate_WhenTransitioningFromCompletedToPublished()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);

        // Act
        round.UpdateStatus(RoundStatus.Published, _dateTimeProvider);

        // Assert
        round.CompletedDateUtc.Should().BeNull();
    }

    [Fact]
    public void UpdateStatus_ShouldClearCompletedDate_WhenTransitioningFromCompletedToInProgress()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);

        // Act
        round.UpdateStatus(RoundStatus.InProgress, _dateTimeProvider);

        // Assert
        round.CompletedDateUtc.Should().BeNull();
    }

    [Fact]
    public void UpdateStatus_ShouldNotSetCompletedDate_WhenTransitioningBetweenNonCompletedStatuses()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.UpdateStatus(RoundStatus.Published, _dateTimeProvider);

        // Assert
        round.CompletedDateUtc.Should().BeNull();
    }

    [Fact]
    public void UpdateStatus_ShouldNotResetCompletedDate_WhenAlreadyCompletedAndStaysCompleted()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);
        var originalCompletedDate = round.CompletedDateUtc;

        // Advance time so we can detect if the date changes
        _dateTimeProvider.AdvanceBy(TimeSpan.FromHours(1));

        // Act
        round.UpdateStatus(RoundStatus.Completed, _dateTimeProvider);

        // Assert — should keep the original date, not update it
        round.CompletedDateUtc.Should().Be(originalCompletedDate);
    }

    [Fact]
    public void UpdateStatus_ShouldUpdateStatusProperty_WhenCalled()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.UpdateStatus(RoundStatus.InProgress, _dateTimeProvider);

        // Assert
        round.Status.Should().Be(RoundStatus.InProgress);
    }

    #endregion

    #region AddMatch

    [Fact]
    public void AddMatch_ShouldAddMatch_WhenValidTeamsProvided()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.AddMatch(1, 2, ValidMatchTime, null);

        // Assert
        round.Matches.Should().HaveCount(1);
    }

    [Fact]
    public void AddMatch_ShouldCreateMatchWithScheduledStatus_WhenAdded()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.AddMatch(1, 2, ValidMatchTime, null);

        // Assert
        round.Matches.First().Status.Should().Be(MatchStatus.Scheduled);
    }

    [Fact]
    public void AddMatch_ShouldSetCorrectTeamIds_WhenAdded()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.AddMatch(1, 2, ValidMatchTime, null);

        // Assert
        var match = round.Matches.First();
        match.HomeTeamId.Should().Be(1);
        match.AwayTeamId.Should().Be(2);
    }

    [Fact]
    public void AddMatch_ShouldThrowException_WhenTeamPlaysItself()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.AddMatch(1, 1, ValidMatchTime, null);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*cannot play against itself*");
    }

    [Fact]
    public void AddMatch_ShouldThrowException_WhenDuplicateMatchExists()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.AddMatch(1, 2, ValidMatchTime, null);

        // Act
        var act = () => round.AddMatch(1, 2, ValidMatchTime, null);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*already exists*");
    }

    [Fact]
    public void AddMatch_ShouldAllowReverseFixture()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.AddMatch(1, 2, ValidMatchTime, null);

        // Act — B vs A is a different fixture
        var act = () => round.AddMatch(2, 1, ValidMatchTime, null);

        // Assert
        act.Should().NotThrow();
        round.Matches.Should().HaveCount(2);
    }

    [Fact]
    public void AddMatch_ShouldAddMultipleMatches_WhenDifferentTeamPairs()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.AddMatch(1, 2, ValidMatchTime, null);
        round.AddMatch(3, 4, ValidMatchTime, null);
        round.AddMatch(5, 6, ValidMatchTime, null);

        // Assert
        round.Matches.Should().HaveCount(3);
    }

    #endregion

    #region RemoveMatch

    [Fact]
    public void RemoveMatch_ShouldRemoveMatch_WhenMatchExists()
    {
        // Arrange — use public constructor so we can set the match ID
        var match = new Match(id: 10, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, placeholderHomeName: null, placeholderAwayName: null);
        var round = new Round(id: 1, seasonId: 1, roundNumber: 1,
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null, lastReminderSentUtc: null,
            matches: new[] { match });

        // Act
        round.RemoveMatch(10);

        // Assert
        round.Matches.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMatch_ShouldDoNothing_WhenMatchDoesNotExist()
    {
        // Arrange
        var match = new Match(id: 10, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, placeholderHomeName: null, placeholderAwayName: null);
        var round = new Round(id: 1, seasonId: 1, roundNumber: 1,
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null, lastReminderSentUtc: null,
            matches: new[] { match });

        // Act
        round.RemoveMatch(999);

        // Assert
        round.Matches.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveMatch_ShouldLeaveOtherMatches_WhenRemovingOne()
    {
        // Arrange
        var match1 = new Match(id: 10, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, placeholderHomeName: null, placeholderAwayName: null);
        var match2 = new Match(id: 11, roundId: 1, homeTeamId: 3, awayTeamId: 4,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, placeholderHomeName: null, placeholderAwayName: null);
        var round = new Round(id: 1, seasonId: 1, roundNumber: 1,
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null, lastReminderSentUtc: null,
            matches: new[] { match1, match2 });

        // Act
        round.RemoveMatch(10);

        // Assert
        round.Matches.Should().HaveCount(1);
        round.Matches.First().Id.Should().Be(11);
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateAllProperties_WhenValid()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        var newStart = ValidStartDate.AddDays(7);
        var newDeadline = ValidDeadline.AddDays(7);

        // Act
        round.UpdateDetails(2, newStart, newDeadline, RoundStatus.Published, "GW2");

        // Assert
        round.RoundNumber.Should().Be(2);
        round.StartDateUtc.Should().Be(newStart);
        round.DeadlineUtc.Should().Be(newDeadline);
        round.Status.Should().Be(RoundStatus.Published);
        round.ApiRoundName.Should().Be("GW2");
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeSeasonId_WhenUpdating()
    {
        // Arrange
        var round = CreateRoundViaFactory(seasonId: 5);

        // Act
        round.UpdateDetails(2, ValidStartDate, ValidDeadline, RoundStatus.Published, "GW2");

        // Assert
        round.SeasonId.Should().Be(5);
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenRoundNumberIsZero()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(0, ValidStartDate, ValidDeadline, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenRoundNumberIsNegative()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(-1, ValidStartDate, ValidDeadline, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenStartDateIsDefault()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, default, ValidDeadline, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineIsDefault()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, ValidStartDate, default, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineAfterStartDate()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, ValidStartDate, ValidStartDate.AddHours(1), RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineEqualsStartDate()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, ValidStartDate, ValidStartDate, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateLastReminderSent

    [Fact]
    public void UpdateLastReminderSent_ShouldSetTimestamp_WhenCalled()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.UpdateLastReminderSent(_dateTimeProvider);

        // Assert
        round.LastReminderSentUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void UpdateLastReminderSent_ShouldUpdateTimestamp_WhenCalledAgain()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.UpdateLastReminderSent(_dateTimeProvider);

        _dateTimeProvider.AdvanceBy(TimeSpan.FromHours(2));

        // Act
        round.UpdateLastReminderSent(_dateTimeProvider);

        // Assert
        round.LastReminderSentUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    #endregion
}
