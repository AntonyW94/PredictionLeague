using FluentAssertions;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

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
            isAlertDismissed: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: _dateTimeProvider.UtcNow,
            roundResults: null);

        // Act
        var act = () => member.Approve(_dateTimeProvider);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending*");
    }

    [Fact]
    public void Approve_ShouldThrowException_WhenRejected()
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Rejected,
            isAlertDismissed: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: null,
            roundResults: null);

        // Act
        var act = () => member.Approve(_dateTimeProvider);

        // Assert
        act.Should().Throw<InvalidOperationException>()
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
            isAlertDismissed: true,
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
            isAlertDismissed: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: _dateTimeProvider.UtcNow,
            roundResults: null);

        // Act
        var act = () => member.Reject();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending*");
    }

    [Fact]
    public void Reject_ShouldThrowException_WhenAlreadyRejected()
    {
        // Arrange
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Rejected,
            isAlertDismissed: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: null,
            roundResults: null);

        // Act
        var act = () => member.Reject();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending*");
    }

    #endregion

    #region DismissAlert

    [Fact]
    public void DismissAlert_ShouldSetIsAlertDismissedToTrue_WhenCalled()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Act
        member.DismissAlert();

        // Assert
        member.IsAlertDismissed.Should().BeTrue();
    }

    [Fact]
    public void DismissAlert_ShouldBeIdempotent_WhenCalledMultipleTimes()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Act
        member.DismissAlert();
        member.DismissAlert();

        // Assert
        member.IsAlertDismissed.Should().BeTrue();
    }

    #endregion
}
