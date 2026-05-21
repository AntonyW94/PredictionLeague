using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class CancelLeagueRequestCommandHandlerTests
{
    private readonly ILeagueMemberRepository _leagueMemberRepository = Substitute.For<ILeagueMemberRepository>();
    private readonly CancelLeagueRequestCommandHandler _handler;

    public CancelLeagueRequestCommandHandlerTests()
    {
        _handler = new CancelLeagueRequestCommandHandler(_leagueMemberRepository);
    }

    private static LeagueMember CreateMember(
        LeagueMemberStatus status = LeagueMemberStatus.Pending)
    {
        return new LeagueMember(
            leagueId: 1, userId: "user-1", status: status,
            isAlertDismissed: false, isArchivedByUser: false,
            joinedAtUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            approvedAtUtc: null, roundResults: null);
    }

    [Fact]
    public async Task Handle_ShouldDeleteMember_WhenRequestIsPending()
    {
        // Arrange
        var member = CreateMember(LeagueMemberStatus.Pending);
        var command = new CancelLeagueRequestCommand(1, "user-1");

        _leagueMemberRepository.GetAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueMemberRepository.Received(1).DeleteAsync(member, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenMemberNotFound()
    {
        // Arrange
        var command = new CancelLeagueRequestCommand(1, "unknown-user");

        _leagueMemberRepository.GetAsync(1, "unknown-user", Arg.Any<CancellationToken>())
            .Returns((LeagueMember?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenRequestIsApproved()
    {
        // Arrange
        var member = CreateMember(LeagueMemberStatus.Approved);
        var command = new CancelLeagueRequestCommand(1, "user-1");

        _leagueMemberRepository.GetAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only cancel requests that are currently pending*");
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenRequestIsRejected()
    {
        // Arrange
        var member = CreateMember(LeagueMemberStatus.Rejected);
        var command = new CancelLeagueRequestCommand(1, "user-1");

        _leagueMemberRepository.GetAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(member);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only cancel requests that are currently pending*");
    }
}
