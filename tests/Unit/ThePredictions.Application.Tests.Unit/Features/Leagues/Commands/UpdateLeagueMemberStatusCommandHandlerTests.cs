using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class UpdateLeagueMemberStatusCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ILeagueMemberRepository _leagueMemberRepository = Substitute.For<ILeagueMemberRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly UpdateLeagueMemberStatusCommandHandler _handler;

    public UpdateLeagueMemberStatusCommandHandlerTests()
    {
        _handler = new UpdateLeagueMemberStatusCommandHandler(
            _leagueRepository, _leagueMemberRepository, _dateTimeProvider);
    }

    private League CreateLeague(int id = 1, string administratorUserId = "admin-user")
    {
        return new League(
            id: id, name: "Test League", seasonId: 1,
            administratorUserId: administratorUserId,
            entryCode: "ABC123",
            createdAtUtc: _dateTimeProvider.UtcNow.AddDays(-30),
            entryDeadlineUtc: _dateTimeProvider.UtcNow.AddMonths(1),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: null, prizeSettings: null);
    }

    private static LeagueMember CreateMember(
        int leagueId = 1,
        string userId = "member-1",
        LeagueMemberStatus status = LeagueMemberStatus.Pending)
    {
        return new LeagueMember(
            leagueId: leagueId, userId: userId, status: status,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            approvedAtUtc: null, roundResults: null);
    }

    [Fact]
    public async Task Handle_ShouldApproveMember_WhenAdministratorApprovesAndMemberIsPending()
    {
        // Arrange
        var league = CreateLeague();
        var member = CreateMember(status: LeagueMemberStatus.Pending);
        var command = new UpdateLeagueMemberStatusCommand(1, "member-1", "admin-user", LeagueMemberStatus.Approved);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _leagueMemberRepository.GetAsync(1, "member-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueMemberRepository.Received(1).UpdateAsync(member, Arg.Any<CancellationToken>());
        member.Status.Should().Be(LeagueMemberStatus.Approved);
    }

    [Fact]
    public async Task Handle_ShouldRejectMember_WhenAdministratorRejects()
    {
        // Arrange
        var league = CreateLeague();
        var member = CreateMember(status: LeagueMemberStatus.Pending);
        var command = new UpdateLeagueMemberStatusCommand(1, "member-1", "admin-user", LeagueMemberStatus.Rejected);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _leagueMemberRepository.GetAsync(1, "member-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueMemberRepository.Received(1).UpdateAsync(member, Arg.Any<CancellationToken>());
        member.Status.Should().Be(LeagueMemberStatus.Rejected);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserIsNotAdministrator()
    {
        // Arrange
        var league = CreateLeague(administratorUserId: "admin-user");
        var command = new UpdateLeagueMemberStatusCommand(1, "member-1", "other-user", LeagueMemberStatus.Approved);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*league administrator*");
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFound()
    {
        // Arrange
        var command = new UpdateLeagueMemberStatusCommand(999, "member-1", "admin-user", LeagueMemberStatus.Approved);

        _leagueRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenMemberNotFound()
    {
        // Arrange
        var league = CreateLeague();
        var command = new UpdateLeagueMemberStatusCommand(1, "unknown-member", "admin-user", LeagueMemberStatus.Approved);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _leagueMemberRepository.GetAsync(1, "unknown-member", Arg.Any<CancellationToken>())
            .Returns((LeagueMember?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenInvalidStatusTransition()
    {
        // Arrange
        var league = CreateLeague();
        var member = CreateMember(status: LeagueMemberStatus.Pending);
        var command = new UpdateLeagueMemberStatusCommand(1, "member-1", "admin-user", (LeagueMemberStatus)99);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _leagueMemberRepository.GetAsync(1, "member-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status change is not permitted*");
    }

    [Fact]
    public async Task Handle_ShouldNotChangeStatus_WhenNewStatusIsPending()
    {
        // Arrange
        var league = CreateLeague();
        var member = CreateMember(status: LeagueMemberStatus.Pending);
        var command = new UpdateLeagueMemberStatusCommand(1, "member-1", "admin-user", LeagueMemberStatus.Pending);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _leagueMemberRepository.GetAsync(1, "member-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        member.Status.Should().Be(LeagueMemberStatus.Pending);
        await _leagueMemberRepository.Received(1).UpdateAsync(member, Arg.Any<CancellationToken>());
    }
}
