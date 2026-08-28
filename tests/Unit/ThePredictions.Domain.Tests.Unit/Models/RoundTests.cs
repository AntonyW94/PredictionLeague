using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class RoundTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    private static readonly DateTime ValidStartDate = new(2025, 8, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ValidDeadline = new(2025, 8, 16, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ValidMatchTime = new(2025, 8, 16, 15, 0, 0, DateTimeKind.Utc);

    private static Round CreateRoundViaFactory(
        int seasonId = 1,
        int roundNumber = 1,
        string displayName = "Gameweek 1",
        DateTime? startDateUtc = null,
        DateTime? deadlineUtc = null,
        string? apiRoundName = null)
    {
        return Round.Create(
            seasonId,
            roundNumber,
            displayName,
            startDateUtc ?? ValidStartDate,
            deadlineUtc ?? ValidDeadline,
            apiRoundName);
    }

    /// <summary>
    /// Creates a round with an explicit ID set (via the public/database constructor)
    /// for tests that call methods requiring a valid ID (e.g. AddMatch).
    /// </summary>
    private static Round CreateRoundWithId(int id = 1)
    {
        return new Round(
            id: id, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null,
            lastReminderSentUtc: null, matches: null);
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
        var act = () => CreateRoundViaFactory(startDateUtc: DateTime.MinValue);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenDeadlineIsDefault()
    {
        // Act
        var act = () => CreateRoundViaFactory(deadlineUtc: DateTime.MinValue);

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
        var round = CreateRoundWithId();

        // Act
        round.AddMatch(1, 2, ValidMatchTime, null);

        // Assert
        round.Matches.Should().HaveCount(1);
    }

    [Fact]
    public void AddMatch_ShouldCreateMatchWithScheduledStatus_WhenAdded()
    {
        // Arrange
        var round = CreateRoundWithId();

        // Act
        round.AddMatch(1, 2, ValidMatchTime, null);

        // Assert
        round.Matches.First().Status.Should().Be(MatchStatus.Scheduled);
    }

    [Fact]
    public void AddMatch_ShouldSetCorrectTeamIds_WhenAdded()
    {
        // Arrange
        var round = CreateRoundWithId();

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
        var round = CreateRoundWithId();

        // Act
        var act = () => round.AddMatch(1, 1, ValidMatchTime, null);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*cannot play against itself*");
    }

    [Fact]
    public void AddMatch_ShouldThrowException_WhenDuplicateMatchExists()
    {
        // Arrange
        var round = CreateRoundWithId();
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
        var round = CreateRoundWithId();
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
        var round = CreateRoundWithId();

        // Act
        round.AddMatch(1, 2, ValidMatchTime, null);
        round.AddMatch(3, 4, ValidMatchTime, null);
        round.AddMatch(5, 6, ValidMatchTime, null);

        // Assert
        round.Matches.Should().HaveCount(3);
    }

    #endregion

    #region AcceptMatch

    [Fact]
    public void AcceptMatch_ShouldAddMatchToRound_WhenMatchIsValid()
    {
        // Arrange
        var round = new Round(id: 5, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null,
            lastReminderSentUtc: null, matches: null);
        var match = new Match(id: 10, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Act
        round.AcceptMatch(match);

        // Assert
        round.Matches.Should().HaveCount(1);
        round.Matches.First().Id.Should().Be(10);
    }

    [Fact]
    public void AcceptMatch_ShouldUpdateMatchRoundId_WhenAccepted()
    {
        // Arrange
        var round = new Round(id: 5, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null,
            lastReminderSentUtc: null, matches: null);
        var match = new Match(id: 10, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Act
        round.AcceptMatch(match);

        // Assert
        round.Matches.First().RoundId.Should().Be(5);
    }

    [Fact]
    public void AcceptMatch_ShouldThrowException_WhenMatchAlreadyExistsInRound()
    {
        // Arrange
        var match = new Match(id: 10, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
        var round = new Round(id: 5, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null,
            lastReminderSentUtc: null, matches: [match]);

        // Act — try to accept the same match again
        var duplicateMatch = new Match(id: 10, roundId: 2, homeTeamId: 3, awayTeamId: 4,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
        var act = () => round.AcceptMatch(duplicateMatch);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*already exists*");
    }

    [Fact]
    public void AcceptMatch_ShouldAcceptMultipleMatches_WhenDifferentIds()
    {
        // Arrange
        var round = new Round(id: 5, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null,
            lastReminderSentUtc: null, matches: null);
        var match1 = new Match(id: 10, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
        var match2 = new Match(id: 11, roundId: 2, homeTeamId: 3, awayTeamId: 4,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

        // Act
        round.AcceptMatch(match1);
        round.AcceptMatch(match2);

        // Assert
        round.Matches.Should().HaveCount(2);
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
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
        var round = new Round(id: 1, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null, lastReminderSentUtc: null,
            matches: [match]);

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
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
        var round = new Round(id: 1, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null, lastReminderSentUtc: null,
            matches: [match]);

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
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
        var match2 = new Match(id: 11, roundId: 1, homeTeamId: 3, awayTeamId: 4,
            matchDateTimeUtc: ValidMatchTime, customLockTimeUtc: null,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
        var round = new Round(id: 1, seasonId: 1, roundNumber: 1, displayName: "Gameweek 1",
            startDateUtc: ValidStartDate, deadlineUtc: ValidDeadline,
            status: RoundStatus.Draft, apiRoundName: null, lastReminderSentUtc: null,
            matches: [match1, match2]);

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
        round.UpdateDetails(2, "Gameweek 2", newStart, newDeadline, RoundStatus.Published, "GW2");

        // Assert
        round.RoundNumber.Should().Be(2);
        round.DisplayName.Should().Be("Gameweek 2");
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
        round.UpdateDetails(2, "Gameweek 2", ValidStartDate, ValidDeadline, RoundStatus.Published, "GW2");

        // Assert
        round.SeasonId.Should().Be(5);
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenRoundNumberIsZero()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(0, "Gameweek 1", ValidStartDate, ValidDeadline, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenRoundNumberIsNegative()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(-1, "Gameweek 1", ValidStartDate, ValidDeadline, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenStartDateIsDefault()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, "Gameweek 1", default, ValidDeadline, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineIsDefault()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, "Gameweek 1", ValidStartDate, default, RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineAfterStartDate()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, "Gameweek 1", ValidStartDate, ValidStartDate.AddHours(1), RoundStatus.Published, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineEqualsStartDate()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        var act = () => round.UpdateDetails(1, "Gameweek 1", ValidStartDate, ValidStartDate, RoundStatus.Published, null);

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

    #region MarkResultsDigestSent

    [Fact]
    public void Create_ShouldSetResultsDigestSentUtcToNull_WhenCreated()
    {
        // Act
        var round = CreateRoundViaFactory();

        // Assert
        round.ResultsDigestSentUtc.Should().BeNull();
    }

    [Fact]
    public void MarkResultsDigestSent_ShouldSetTimestamp_WhenCalled()
    {
        // Arrange
        var round = CreateRoundViaFactory();

        // Act
        round.MarkResultsDigestSent(_dateTimeProvider);

        // Assert
        round.ResultsDigestSentUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void MarkResultsDigestSent_ShouldUpdateTimestamp_WhenCalledAgain()
    {
        // Arrange
        var round = CreateRoundViaFactory();
        round.MarkResultsDigestSent(_dateTimeProvider);

        _dateTimeProvider.AdvanceBy(TimeSpan.FromHours(2));

        // Act
        round.MarkResultsDigestSent(_dateTimeProvider);

        // Assert
        round.ResultsDigestSentUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    #endregion

    #region DisplayName

    [Fact]
    public void Create_ShouldSetDisplayName()
    {
        // Act
        var round = CreateRoundViaFactory(displayName: "Round of 16");

        // Assert
        round.DisplayName.Should().Be("Round of 16");
    }

    [Fact]
    public void Create_ShouldThrow_WhenDisplayNameIsEmpty()
    {
        // Act
        var act = () => CreateRoundViaFactory(displayName: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenDisplayNameIsWhitespace()
    {
        // Act
        var act = () => CreateRoundViaFactory(displayName: " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldUpdateDisplayName()
    {
        // Arrange
        var round = CreateRoundViaFactory(displayName: "Gameweek 1");

        // Act
        round.UpdateDetails(1, "Quarter-Final", ValidStartDate, ValidDeadline, RoundStatus.Draft, null);

        // Assert
        round.DisplayName.Should().Be("Quarter-Final");
    }

    #endregion

    #region AddPlaceholderMatch

    [Fact]
    public void AddPlaceholderMatch_ShouldAddMatch_WhenValidParametersProvided()
    {
        // Arrange
        var round = CreateRoundWithId();

        // Act
        round.AddPlaceholderMatch("Semi-final 1", "Semi-final 1", "Semi-finals");

        // Assert
        round.Matches.Should().HaveCount(1);
        var match = round.Matches.First();
        match.HomeTeamId.Should().BeNull();
        match.AwayTeamId.Should().BeNull();
        match.PlaceholderHomeName.Should().Be("Semi-final 1");
        match.PlaceholderAwayName.Should().Be("Semi-final 1");
        match.AreTeamsConfirmed.Should().BeFalse();
    }

    [Fact]
    public void AddPlaceholderMatch_ShouldAddMultiplePlaceholders_WhenCalledMultipleTimes()
    {
        // Arrange
        var round = CreateRoundWithId();

        // Act
        round.AddPlaceholderMatch("SF 1", "SF 1", "Semi-finals");
        round.AddPlaceholderMatch("SF 2", "SF 2", "Semi-finals");
        round.AddPlaceholderMatch("Final", "Final", "Final");

        // Assert
        round.Matches.Should().HaveCount(3);
    }

    #endregion

    #region HasConfirmedFixtures

    [Fact]
    public void HasConfirmedFixtures_ShouldBeFalse_WhenRoundHasNoMatches()
    {
        // Arrange
        var round = CreateRoundWithId();

        // Act / Assert
        round.HasConfirmedFixtures.Should().BeFalse();
    }

    [Fact]
    public void HasConfirmedFixtures_ShouldBeFalse_WhenAllMatchesArePlaceholders()
    {
        // Arrange
        var round = CreateRoundWithId();
        round.AddPlaceholderMatch("Winner QF1", "Winner QF2", "Semi-finals");
        round.AddPlaceholderMatch("Winner QF3", "Winner QF4", "Semi-finals");

        // Act / Assert
        round.HasConfirmedFixtures.Should().BeFalse();
    }

    [Fact]
    public void HasConfirmedFixtures_ShouldBeTrue_WhenAtLeastOneMatchHasBothTeams()
    {
        // Arrange
        var round = CreateRoundWithId();
        round.AddPlaceholderMatch("Winner QF1", "Winner QF2", "Semi-finals");
        round.AddMatch(1, 2, ValidMatchTime, externalId: null);

        // Act / Assert
        round.HasConfirmedFixtures.Should().BeTrue();
    }

    #endregion

    #region GetLatestPredictionDeadline / IsClosedForPredictions

    private static Match CreateMatchWithCustomLock(int id, DateTime matchDateTimeUtc, DateTime? customLockTimeUtc) =>
        new(id: id, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: matchDateTimeUtc,
            customLockTimeUtc: customLockTimeUtc,
            status: MatchStatus.Scheduled, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

    private static Round CreateRoundWithMatches(DateTime deadlineUtc, params DateTime?[] customLockTimes)
    {
        var matches = customLockTimes
            .Select((lockTime, index) => CreateMatchWithCustomLock(index + 1, deadlineUtc.AddHours(1), lockTime))
            .ToList<Match>();

        return new Round(
            id: 1, seasonId: 1, roundNumber: 1, displayName: "Finals",
            startDateUtc: deadlineUtc.AddHours(2), deadlineUtc: deadlineUtc,
            status: RoundStatus.Published, apiRoundName: null, lastReminderSentUtc: null,
            matches: matches);
    }

    [Fact]
    public void GetLatestPredictionDeadline_ShouldReturnRoundDeadline_WhenRoundHasNoMatches()
    {
        // Arrange
        var round = CreateRoundWithId();

        // Act
        var result = round.GetLatestPredictionDeadline();

        // Assert
        result.Should().Be(round.DeadlineUtc);
    }

    [Fact]
    public void GetLatestPredictionDeadline_ShouldReturnRoundDeadline_WhenNoMatchHasLaterCustomLock()
    {
        // Arrange - one match with no custom lock, one locking before the round deadline
        var deadline = new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
        var round = CreateRoundWithMatches(deadline, null, deadline.AddHours(-1));

        // Act
        var result = round.GetLatestPredictionDeadline();

        // Assert
        result.Should().Be(deadline);
    }

    [Fact]
    public void GetLatestPredictionDeadline_ShouldReturnLatestCustomLock_WhenMatchLocksAfterRoundDeadline()
    {
        // Arrange - semi-finals lock at the round deadline, the final locks three days later
        var deadline = new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
        var finalLock = new DateTime(2026, 7, 19, 18, 30, 0, DateTimeKind.Utc);
        var round = CreateRoundWithMatches(deadline, null, finalLock);

        // Act
        var result = round.GetLatestPredictionDeadline();

        // Assert
        result.Should().Be(finalLock);
    }

    [Fact]
    public void GetLatestPredictionDeadline_ShouldIgnoreAPostponedMatchesLaterCustomLock()
    {
        // A called-off fixture cannot be predicted, so it must not hold the round open. This is the only shape where the
        // three answers to "when does this round close" could differ: a fixture carrying both a custom lock later than the
        // round deadline and the postponed status.
        var deadline = new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
        var round = CreateRoundWithPostponedLateLock(deadline, postponedLockUtc: deadline.AddDays(5));

        // Act
        var result = round.GetLatestPredictionDeadline();

        // Assert
        result.Should().Be(deadline);
    }

    [Fact]
    public void IsClosedForPredictions_ShouldReturnTrue_WhenOnlyAPostponedMatchWouldStillBeOpen()
    {
        // Arrange - the consequence of the above: the round is finished with players, so the reminder job stops chasing it
        // and nothing more can be submitted against it.
        var deadline = _dateTimeProvider.UtcNow.AddHours(-1);
        var round = CreateRoundWithPostponedLateLock(deadline, postponedLockUtc: _dateTimeProvider.UtcNow.AddDays(5));

        // Act
        var result = round.IsClosedForPredictions(_dateTimeProvider.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>A round whose only late-locking fixture has been called off.</summary>
    private static Round CreateRoundWithPostponedLateLock(DateTime deadlineUtc, DateTime postponedLockUtc)
    {
        var scheduled = CreateMatchWithCustomLock(1, deadlineUtc.AddHours(1), null);

        var postponed = new Match(
            id: 2, roundId: 1, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: postponedLockUtc.AddHours(1),
            customLockTimeUtc: postponedLockUtc,
            status: MatchStatus.Postponed, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null, placeholderHomeName: null, placeholderAwayName: null,
            apiRoundName: null);

        return new Round(
            id: 1, seasonId: 1, roundNumber: 1, displayName: "Finals",
            startDateUtc: deadlineUtc.AddHours(2), deadlineUtc: deadlineUtc,
            status: RoundStatus.Published, apiRoundName: null, lastReminderSentUtc: null,
            matches: [scheduled, postponed]);
    }

    [Fact]
    public void IsClosedForPredictions_ShouldReturnTrue_WhenLatestDeadlineHasPassed()
    {
        // Arrange
        var deadline = _dateTimeProvider.UtcNow.AddDays(-2);
        var round = CreateRoundWithMatches(deadline, null, deadline.AddHours(-1));

        // Act
        var result = round.IsClosedForPredictions(_dateTimeProvider.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsClosedForPredictions_ShouldReturnFalse_WhenAMatchIsStillOpenViaCustomLock()
    {
        // Arrange - the round deadline has passed but the final still locks in the future
        var deadline = _dateTimeProvider.UtcNow.AddDays(-2);
        var round = CreateRoundWithMatches(deadline, null, _dateTimeProvider.UtcNow.AddDays(1));

        // Act
        var result = round.IsClosedForPredictions(_dateTimeProvider.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasStaggeredPredictionDeadlines

    [Fact]
    public void HasStaggeredPredictionDeadlines_ShouldBeFalse_WhenRoundHasNoMatches()
    {
        // Arrange
        var round = CreateRoundWithId();

        // Act / Assert
        round.HasStaggeredPredictionDeadlines.Should().BeFalse();
    }

    [Fact]
    public void HasStaggeredPredictionDeadlines_ShouldBeFalse_WhenEveryMatchLocksAtTheRoundDeadline()
    {
        // Arrange - an ordinary league round: no custom locks, so the whole round is predicted in one go
        var deadline = new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
        var round = CreateRoundWithMatches(deadline, null, null, null);

        // Act / Assert
        round.HasStaggeredPredictionDeadlines.Should().BeFalse();
    }

    [Fact]
    public void HasStaggeredPredictionDeadlines_ShouldBeFalse_WhenEveryCustomLockIsTheSameMoment()
    {
        // Arrange - custom locks that all agree still leave one deadline, so the round is not split
        var deadline = new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
        var sharedLock = deadline.AddHours(3);
        var round = CreateRoundWithMatches(deadline, sharedLock, sharedLock);

        // Act / Assert
        round.HasStaggeredPredictionDeadlines.Should().BeFalse();
    }

    [Fact]
    public void HasStaggeredPredictionDeadlines_ShouldBeTrue_WhenAMatchCarriesItsOwnLock()
    {
        // Arrange - a combined round: the semi-finals lock at the round deadline, the final three days later
        var deadline = new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
        var round = CreateRoundWithMatches(deadline, null, deadline.AddDays(3));

        // Act / Assert
        round.HasStaggeredPredictionDeadlines.Should().BeTrue();
    }

    [Fact]
    public void HasStaggeredPredictionDeadlines_ShouldIgnoreAPostponedMatchesOwnLock()
    {
        // A called-off fixture cannot be predicted, so a stale lock on one must not make an ordinary round
        // look like it is played in batches.
        var deadline = new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
        var round = CreateRoundWithPostponedLateLock(deadline, postponedLockUtc: deadline.AddDays(5));

        // Act / Assert
        round.HasStaggeredPredictionDeadlines.Should().BeFalse();
    }

    #endregion

    #region RecalculateBatchPredictionLocks

    private static readonly DateTime BatchRoundDeadline = new(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime SemiFinal1Kickoff = new(2026, 7, 14, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SemiFinal2Kickoff = new(2026, 7, 15, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinalKickoff = new(2026, 7, 18, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ThirdPlaceKickoff = new(2026, 7, 19, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExpectedBatchLock = new(2026, 7, 18, 20, 30, 0, DateTimeKind.Utc);

    private static Match BatchMatch(int id, string? apiRoundName, DateTime kickoffUtc, DateTime? customLockTimeUtc = null, MatchStatus status = MatchStatus.Scheduled, bool confirmed = true) =>
        new(id: id, roundId: 1,
            homeTeamId: confirmed ? 1 : null, awayTeamId: confirmed ? 2 : null,
            matchDateTimeUtc: kickoffUtc, customLockTimeUtc: customLockTimeUtc,
            status: status, actualHomeTeamScore: null, actualAwayTeamScore: null,
            externalId: null, matchNumber: null,
            placeholderHomeName: confirmed ? null : "TBC", placeholderAwayName: confirmed ? null : "TBC",
            apiRoundName: apiRoundName);

    private static Round CreateBatchRound(params Match[] matches) =>
        new(id: 1, seasonId: 1, roundNumber: 1, displayName: "Finals",
            startDateUtc: SemiFinal1Kickoff, deadlineUtc: BatchRoundDeadline,
            status: RoundStatus.InProgress, apiRoundName: null, lastReminderSentUtc: null,
            matches: matches.ToList());

    [Fact]
    public void RecalculateBatchPredictionLocks_ShouldLockFinalBatchTogether_WhenCombinedRound()
    {
        // Arrange - semi-finals (earliest batch) plus a final and third-place playoff that should batch
        // together. The third-place playoff starts with a stale lock 30 minutes before its own kickoff.
        var semiFinal1 = BatchMatch(1, "Semi-finals", SemiFinal1Kickoff, status: MatchStatus.Completed);
        var semiFinal2 = BatchMatch(2, "Semi-finals", SemiFinal2Kickoff, status: MatchStatus.Completed);
        var final = BatchMatch(3, "Final", FinalKickoff, customLockTimeUtc: ExpectedBatchLock);
        var thirdPlace = BatchMatch(4, "3rd Place Final", ThirdPlaceKickoff, customLockTimeUtc: ThirdPlaceKickoff.AddMinutes(-30));
        var round = CreateBatchRound(semiFinal1, semiFinal2, final, thirdPlace);

        // Act
        var changed = round.RecalculateBatchPredictionLocks();

        // Assert - the earliest batch (semi-finals) has no custom lock; the final and third-place playoff
        // both lock together 30 minutes before the earlier of the two.
        changed.Should().BeTrue();
        semiFinal1.CustomLockTimeUtc.Should().BeNull();
        semiFinal2.CustomLockTimeUtc.Should().BeNull();
        final.CustomLockTimeUtc.Should().Be(ExpectedBatchLock);
        thirdPlace.CustomLockTimeUtc.Should().Be(ExpectedBatchLock);
    }

    [Fact]
    public void RecalculateBatchPredictionLocks_ShouldIgnoreUnconfirmedPostponedAndUnparseableMatches()
    {
        // Arrange - only the semi-final and final are eligible; the rest must be ignored. The postponed
        // third-place playoff kicks off earliest, so if it were wrongly included the final batch would lock
        // before its kickoff instead of the final's.
        var semiFinal = BatchMatch(1, "Semi-finals", SemiFinal1Kickoff, status: MatchStatus.Completed);
        var final = BatchMatch(2, "Final", FinalKickoff);
        var placeholder = BatchMatch(3, "Final", DateTime.MaxValue, confirmed: false);
        var postponed = BatchMatch(4, "3rd Place Final", new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc), status: MatchStatus.Postponed);
        var noApiRoundName = BatchMatch(5, null, FinalKickoff);
        var unparseable = BatchMatch(6, "Not A Real Stage", FinalKickoff);
        var round = CreateBatchRound(semiFinal, final, placeholder, postponed, noApiRoundName, unparseable);

        // Act
        var changed = round.RecalculateBatchPredictionLocks();

        // Assert
        changed.Should().BeTrue();
        final.CustomLockTimeUtc.Should().Be(FinalKickoff.AddMinutes(-30));
        semiFinal.CustomLockTimeUtc.Should().BeNull();
        placeholder.CustomLockTimeUtc.Should().BeNull();
        postponed.CustomLockTimeUtc.Should().BeNull();
    }

    [Fact]
    public void RecalculateBatchPredictionLocks_ShouldReturnFalse_WhenNoConfirmedMatchesToBatch()
    {
        // Arrange - only unassigned placeholders, nothing to batch
        var round = CreateBatchRound(
            BatchMatch(1, "Semi-finals", DateTime.MaxValue, confirmed: false),
            BatchMatch(2, "Final", DateTime.MaxValue, confirmed: false));

        // Act
        var changed = round.RecalculateBatchPredictionLocks();

        // Assert
        changed.Should().BeFalse();
    }

    [Fact]
    public void RecalculateBatchPredictionLocks_ShouldReturnFalse_WhenAllLockTimesAlreadyCorrect()
    {
        // Arrange - locks already match what the batch calculation would produce
        var semiFinal = BatchMatch(1, "Semi-finals", SemiFinal1Kickoff, status: MatchStatus.Completed);
        var final = BatchMatch(2, "Final", FinalKickoff, customLockTimeUtc: ExpectedBatchLock);
        var thirdPlace = BatchMatch(3, "3rd Place Final", ThirdPlaceKickoff, customLockTimeUtc: ExpectedBatchLock);
        var round = CreateBatchRound(semiFinal, final, thirdPlace);

        // Act
        var changed = round.RecalculateBatchPredictionLocks();

        // Assert
        changed.Should().BeFalse();
    }

    #endregion

    #region GetNextPredictionDeadline

    [Fact]
    public void GetNextPredictionDeadline_ShouldReturnRoundDeadline_WhenMatchesLockAtRoundDeadline()
    {
        // Arrange - a normal round: every match locks at the round deadline
        var round = CreateBatchRound(
            BatchMatch(1, "Semi-finals", SemiFinal1Kickoff),
            BatchMatch(2, "Semi-finals", SemiFinal2Kickoff));

        // Act
        var result = round.GetNextPredictionDeadline(BatchRoundDeadline.AddDays(-1));

        // Assert
        result.Should().Be(BatchRoundDeadline);
    }

    [Fact]
    public void GetNextPredictionDeadline_ShouldReturnEarliestFutureLock_WhenBatchesLockAtDifferentTimes()
    {
        // Arrange - the round deadline (semi-finals) has passed; the final and third-place playoff carry
        // later locks and one match locks later still.
        var now = BatchRoundDeadline.AddDays(1);
        var laterLock = new DateTime(2026, 7, 19, 18, 30, 0, DateTimeKind.Utc);
        var round = CreateBatchRound(
            BatchMatch(1, "Semi-finals", SemiFinal1Kickoff, status: MatchStatus.Completed),
            BatchMatch(2, "Final", FinalKickoff, customLockTimeUtc: laterLock),
            BatchMatch(3, "3rd Place Final", ThirdPlaceKickoff, customLockTimeUtc: ExpectedBatchLock),
            BatchMatch(4, "Final", FinalKickoff, customLockTimeUtc: ExpectedBatchLock));

        // Act
        var result = round.GetNextPredictionDeadline(now);

        // Assert - the earliest lock still in the future
        result.Should().Be(ExpectedBatchLock);
    }

    [Fact]
    public void GetNextPredictionDeadline_ShouldReturnNull_WhenEveryMatchHasLocked()
    {
        // Arrange - now is after every lock
        var round = CreateBatchRound(
            BatchMatch(1, "Semi-finals", SemiFinal1Kickoff),
            BatchMatch(2, "Final", FinalKickoff, customLockTimeUtc: ExpectedBatchLock));

        // Act
        var result = round.GetNextPredictionDeadline(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetNextPredictionDeadline_ShouldIgnorePostponedAndUnconfirmedMatches()
    {
        // Arrange - only the confirmed, scheduled final should be considered
        var round = CreateBatchRound(
            BatchMatch(1, "Final", FinalKickoff, customLockTimeUtc: ExpectedBatchLock),
            BatchMatch(2, "Semi-finals", SemiFinal1Kickoff, status: MatchStatus.Postponed),
            BatchMatch(3, "Final", DateTime.MaxValue, confirmed: false));

        // Act
        var result = round.GetNextPredictionDeadline(BatchRoundDeadline.AddDays(-1));

        // Assert
        result.Should().Be(ExpectedBatchLock);
    }

    [Fact]
    public void GetNextPredictionDeadline_ShouldIgnoreAFixtureThatHasKickedOff_EvenIfItsLockIsSomehowAhead()
    {
        // A match cannot legitimately be in progress before its own prediction lock, so this state means
        // something has gone wrong upstream. The rule assumes it away rather than defending against it, which
        // is what lets this method and Match.IsOpenForPrediction share one definition of "still open".
        var round = CreateBatchRound(
            BatchMatch(1, "Final", FinalKickoff, customLockTimeUtc: ExpectedBatchLock, status: MatchStatus.InProgress));

        round.GetNextPredictionDeadline(BatchRoundDeadline.AddDays(-1)).Should().BeNull();
    }

    [Fact]
    public void GetNextPredictionDeadline_ShouldIgnoreACompletedFixture_EvenIfItsLockIsSomehowAhead()
    {
        var round = CreateBatchRound(
            BatchMatch(1, "Final", FinalKickoff, customLockTimeUtc: ExpectedBatchLock, status: MatchStatus.Completed));

        round.GetNextPredictionDeadline(BatchRoundDeadline.AddDays(-1)).Should().BeNull();
    }

    #endregion
}
