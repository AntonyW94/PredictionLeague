using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeagueMemberTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateMember_WhenValidParametersProvided()
    {
        // Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.LeagueId.Should().Be(1);
        member.UserId.Should().Be("user-1");
    }

    [Fact]
    public void Create_ShouldSetStatusToPending_WhenCreated()
    {
        // Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.Status.Should().Be(LeagueMemberStatus.Pending);
    }

    [Fact]
    public void Create_ShouldSetIsAlertDismissedToFalse_WhenCreated()
    {
        // Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.IsAlertDismissed.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetApprovedAtUtcToNull_WhenCreated()
    {
        // Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.ApprovedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetJoinedAtUtc_WhenCreated()
    {
        // Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.JoinedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldInitialiseEmptyRoundResultsCollection_WhenCreated()
    {
        // Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.RoundResults.Should().BeEmpty();
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenLeagueIdIsZero()
    {
        // Act
        var act = () => LeagueMember.Create(0, "user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLeagueIdIsNegative()
    {
        // Act
        var act = () => LeagueMember.Create(-1, "user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsNull()
    {
        // Act
        var act = () => LeagueMember.Create(1, null!, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => LeagueMember.Create(1, "", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Act
        var act = () => LeagueMember.Create(1, " ", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Approve

    [Fact]
    public void Approve_ShouldSetStatusToApproved_WhenPending()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Act
        member.Approve(_dateTimeProvider);

        // Assert
        member.Status.Should().Be(LeagueMemberStatus.Approved);
    }

    [Fact]
    public void Approve_ShouldSetApprovedAtUtc_WhenPending()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);
        var approvalTime = new DateTime(2025, 6, 16, 10, 0, 0, DateTimeKind.Utc);
        _dateTimeProvider.UtcNow = approvalTime;

        // Act
        member.Approve(_dateTimeProvider);

        // Assert
        member.ApprovedAtUtc.Should().Be(approvalTime);
    }

    [Fact]
    public void Approve_ShouldThrowException_WhenAlreadyApproved()
    {
        // Arrange — use public constructor to set up Approved status
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: _dateTimeProvider.UtcNow,
            roundResults: null);

        // Act
        var act = () => member.Approve(_dateTimeProvider);

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*pending*");
    }

    [Fact]
    public void Approve_ShouldThrowException_WhenRejected()
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Rejected,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: null,
            roundResults: null);

        // Act
        var act = () => member.Approve(_dateTimeProvider);

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*pending*");
    }

    #endregion

    #region Reject

    [Fact]
    public void Reject_ShouldSetStatusToRejected_WhenPending()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Act
        member.Reject();

        // Assert
        member.Status.Should().Be(LeagueMemberStatus.Rejected);
    }

    [Fact]
    public void Reject_ShouldResetIsAlertDismissed_WhenPending()
    {
        // Arrange — use public constructor with IsAlertDismissed = true
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Pending,
            isAlertDismissed: true, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: null,
            roundResults: null);

        // Act
        member.Reject();

        // Assert
        member.IsAlertDismissed.Should().BeFalse();
    }

    [Fact]
    public void Reject_ShouldSetIsAlertDismissedToFalse_WhenAlreadyFalse()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Act
        member.Reject();

        // Assert
        member.IsAlertDismissed.Should().BeFalse();
    }

    [Fact]
    public void Reject_ShouldThrowException_WhenAlreadyApproved()
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: _dateTimeProvider.UtcNow,
            roundResults: null);

        // Act
        var act = () => member.Reject();

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*pending*");
    }

    [Fact]
    public void Reject_ShouldThrowException_WhenAlreadyRejected()
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Rejected,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: null,
            roundResults: null);

        // Act
        var act = () => member.Reject();

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*pending*");
    }

    #endregion

    #region DismissAlert

    private LeagueMember RejectedMember()
    {
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);
        member.Reject();

        return member;
    }

    [Fact]
    public void DismissAlert_ShouldSetIsAlertDismissedToTrue_WhenTheMemberWasRejected()
    {
        // Arrange
        var member = RejectedMember();

        // Act
        member.DismissAlert();

        // Assert
        member.IsAlertDismissed.Should().BeTrue();
    }

    [Fact]
    public void DismissAlert_ShouldBeIdempotent_WhenCalledMultipleTimes()
    {
        // Arrange
        var member = RejectedMember();

        // Act
        member.DismissAlert();
        member.DismissAlert();

        // Assert
        member.IsAlertDismissed.Should().BeTrue();
    }

    // Only a rejected membership has an alert to hide. Dismissing on any other status would set a flag
    // nothing reads, and the rule used to live in the command handler where a second caller could miss it.
    [Theory]
    [InlineData(LeagueMemberStatus.Pending)]
    [InlineData(LeagueMemberStatus.Approved)]
    public void DismissAlert_ShouldThrow_WhenTheMemberWasNotRejected(LeagueMemberStatus status)
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: status,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow, approvedAtUtc: null, roundResults: null);

        // Act
        var act = member.DismissAlert;

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("This notification cannot be dismissed.");
        member.IsAlertDismissed.Should().BeFalse();
    }

    #endregion

    #region EnsureJoinRequestCanBeCancelled

    [Fact]
    public void EnsureJoinRequestCanBeCancelled_ShouldNotThrow_WhenTheRequestIsStillPending()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Act
        var act = member.EnsureJoinRequestCanBeCancelled;

        // Assert
        act.Should().NotThrow();
    }

    // Withdrawing is for a request still waiting on the admin. Once approved or rejected the row is no
    // longer the member's to remove, which is what stops a cancel deleting a live membership.
    [Theory]
    [InlineData(LeagueMemberStatus.Approved)]
    [InlineData(LeagueMemberStatus.Rejected)]
    public void EnsureJoinRequestCanBeCancelled_ShouldThrow_WhenTheRequestIsNoLongerPending(LeagueMemberStatus status)
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: status,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow, approvedAtUtc: null, roundResults: null);

        // Act
        var act = member.EnsureJoinRequestCanBeCancelled;

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("You can only cancel requests that are currently pending.");
    }

    #endregion

    #region Archive / Unarchive

    private LeagueMember ApprovedMember() => new(
        leagueId: 1, userId: "user-1",
        status: LeagueMemberStatus.Approved,
        isAlertDismissed: false, isArchivedByUser: false,
        joinedAtUtc: _dateTimeProvider.UtcNow,
        approvedAtUtc: _dateTimeProvider.UtcNow,
        roundResults: null);

    [Fact]
    public void Create_ShouldSetIsArchivedByUserToFalse_WhenCreated()
    {
        // Arrange / Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.IsArchivedByUser.Should().BeFalse();
    }

    [Fact]
    public void Archive_ShouldSetIsArchivedByUserToTrue_WhenMemberIsApproved()
    {
        // Arrange
        var member = ApprovedMember();

        // Act
        member.Archive();

        // Assert
        member.IsArchivedByUser.Should().BeTrue();
    }

    [Fact]
    public void Archive_ShouldThrowException_WhenMemberIsPending()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Act
        var act = () => member.Archive();

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public void Archive_ShouldThrowException_WhenMemberIsRejected()
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Rejected,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: null,
            roundResults: null);

        // Act
        var act = () => member.Archive();

        // Assert
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public void Unarchive_ShouldSetIsArchivedByUserToFalse_WhenCalled()
    {
        // Arrange
        var member = ApprovedMember();
        member.Archive();

        // Act
        member.Unarchive();

        // Assert
        member.IsArchivedByUser.Should().BeFalse();
    }

    [Fact]
    public void Unarchive_ShouldBeIdempotent_WhenAlreadyUnarchived()
    {
        // Arrange
        var member = ApprovedMember();

        // Act
        member.Unarchive();

        // Assert
        member.IsArchivedByUser.Should().BeFalse();
    }

    #endregion
}
