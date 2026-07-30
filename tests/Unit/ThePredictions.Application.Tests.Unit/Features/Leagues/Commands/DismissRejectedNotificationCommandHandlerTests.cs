using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class DismissRejectedNotificationCommandHandlerTests
{
    private readonly ILeagueMemberRepository _leagueMemberRepository = Substitute.For<ILeagueMemberRepository>();
    private readonly DismissRejectedNotificationCommandHandler _handler;

    public DismissRejectedNotificationCommandHandlerTests()
    {
        _handler = new DismissRejectedNotificationCommandHandler(_leagueMemberRepository);
    }

    private static LeagueMember CreateMember(
        LeagueMemberStatus status = LeagueMemberStatus.Rejected)
    {
        return new LeagueMember(
            leagueId: 1, userId: "user-1", status: status,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            approvedAtUtc: null, roundResults: null);
    }

    [Fact]
    public async Task Handle_ShouldDismissAlertAndUpdate_WhenMemberIsRejected()
    {
        // Arrange
        var member = CreateMember(LeagueMemberStatus.Rejected);
        var command = new DismissRejectedNotificationCommand(1, "user-1");

        _leagueMemberRepository.GetAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        member.IsAlertDismissed.Should().BeTrue();
        await _leagueMemberRepository.Received(1).UpdateAsync(member, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenMemberNotFound()
    {
        // Arrange
        var command = new DismissRejectedNotificationCommand(1, "unknown-user");

        _leagueMemberRepository.GetAsync(1, "unknown-user", Arg.Any<CancellationToken>())
            .Returns((LeagueMember?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenMemberIsNotRejected()
    {
        // Arrange
        var member = CreateMember(LeagueMemberStatus.Pending);
        var command = new DismissRejectedNotificationCommand(1, "user-1");

        _leagueMemberRepository.GetAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .WithMessage("*notification cannot be dismissed*");
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenMemberIsApproved()
    {
        // Arrange
        var member = CreateMember(LeagueMemberStatus.Approved);
        var command = new DismissRejectedNotificationCommand(1, "user-1");

        _leagueMemberRepository.GetAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .WithMessage("*notification cannot be dismissed*");
    }
}
