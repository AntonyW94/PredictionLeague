using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class RemoveRejectedLeagueCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly RemoveRejectedLeagueCommandHandler _handler;

    public RemoveRejectedLeagueCommandHandlerTests()
    {
        _handler = new RemoveRejectedLeagueCommandHandler(_leagueRepository);
    }

    private static League CreateLeagueWithMember(
        string memberUserId,
        LeagueMemberStatus memberStatus)
    {
        var member = new LeagueMember(
            leagueId: 1, userId: memberUserId, status: memberStatus,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            approvedAtUtc: null, roundResults: null);

        return new League(
            id: 1, name: "Test League", seasonId: 1,
            administratorUserId: "admin-user",
            entryCode: "ABC123",
            createdAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            entryDeadlineUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: new[] { member }, prizeSettings: null);
    }

    [Fact]
    public async Task Handle_ShouldRemoveMemberAndUpdateLeague_WhenMemberIsRejected()
    {
        // Arrange
        var league = CreateLeagueWithMember("user-1", LeagueMemberStatus.Rejected);
        var command = new RemoveRejectedLeagueCommand(1, "user-1");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFound()
    {
        // Arrange
        var command = new RemoveRejectedLeagueCommand(999, "user-1");

        _leagueRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenUserIsNotMemberOfLeague()
    {
        // Arrange
        var league = CreateLeagueWithMember("other-user", LeagueMemberStatus.Rejected);
        var command = new RemoveRejectedLeagueCommand(1, "non-member");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only remove leagues with a 'Rejected' status*");
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenMemberIsNotRejected()
    {
        // Arrange
        var league = CreateLeagueWithMember("user-1", LeagueMemberStatus.Approved);
        var command = new RemoveRejectedLeagueCommand(1, "user-1");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only remove leagues with a 'Rejected' status*");
    }
}
