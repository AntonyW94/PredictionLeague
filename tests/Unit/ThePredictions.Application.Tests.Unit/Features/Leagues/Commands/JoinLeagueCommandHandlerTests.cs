using FluentAssertions;
using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class JoinLeagueCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonAccessService _seasonAccessService = Substitute.For<ISeasonAccessService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly JoinLeagueCommandHandler _handler;

    public JoinLeagueCommandHandlerTests()
    {
        _handler = new JoinLeagueCommandHandler(_leagueRepository, _seasonAccessService, _mediator, _dateTimeProvider);
    }

    private League CreateLeague(int id = 1, string administratorUserId = "admin-user", DateTime? entryDeadlineUtc = null, string? entryCode = null, bool requiresMemberApproval = true)
    {
        return new League(
            id: id, name: "Test League", seasonId: 1,
            administratorUserId: administratorUserId,
            entryCode: entryCode,
            createdAtUtc: _dateTimeProvider.UtcNow.AddDays(-30),
            entryDeadlineUtc: entryDeadlineUtc ?? _dateTimeProvider.UtcNow.AddMonths(1),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: null, prizeSettings: null,
            requiresMemberApproval: requiresMemberApproval);
    }

    [Fact]
    public async Task Handle_ShouldAddMemberAndUpdateLeague_WhenJoiningByLeagueId()
    {
        // Arrange
        var league = CreateLeague(id: 5);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAddMemberAndUpdateLeague_WhenJoiningByEntryCode()
    {
        // Arrange
        var league = CreateLeague(id: 5, entryCode: "ABC123");
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: null, EntryCode: "ABC123");

        _leagueRepository.GetByEntryCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFoundById()
    {
        // Arrange
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 999, EntryCode: null);

        _leagueRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFoundByEntryCode()
    {
        // Arrange
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: null, EntryCode: "INVALID");

        _leagueRepository.GetByEntryCodeAsync("INVALID", Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenNoLeagueIdOrEntryCode()
    {
        // Arrange
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: null, EntryCode: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LeagueId*EntryCode*");
    }

    [Fact]
    public async Task Handle_ShouldSendNotification_WhenMemberSuccessfullyAdded()
    {
        // Arrange
        var league = CreateLeague(id: 5);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<NotifyLeagueAdminOfJoinRequestCommand>(n =>
                n.AdministratorUserId == "admin-user" &&
                n.LeagueName == "Test League" &&
                n.SeasonId == 1 &&
                n.NewMemberFirstName == "Jane" &&
                n.NewMemberLastName == "Doe"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPreferLeagueId_WhenBothLeagueIdAndEntryCodeProvided()
    {
        // Arrange
        var league = CreateLeague(id: 5);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: "ABC123");

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).GetByIdAsync(5, Arg.Any<CancellationToken>());
        await _leagueRepository.DidNotReceive().GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenJoiningPrivateLeagueByLeagueId()
    {
        // Arrange — listing a private league exposes its id, so the by-id path must reject it (code required)
        var league = CreateLeague(id: 5, entryCode: "ABC123");
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entry code*");
        await _leagueRepository.DidNotReceive().UpdateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotifyMember_WhenLeagueDoesNotRequireApproval()
    {
        // Arrange — approval off: the joiner is auto-approved and told they can take part
        var league = CreateLeague(id: 5, requiresMemberApproval: false);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<NotifyMemberOfLeagueApprovalCommand>(n =>
                n.MemberUserId == "new-user" &&
                n.LeagueId == 5 &&
                n.LeagueName == "Test League" &&
                n.SeasonId == 1),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(Arg.Any<NotifyLeagueAdminOfJoinRequestCommand>(), Arg.Any<CancellationToken>());
    }
}
