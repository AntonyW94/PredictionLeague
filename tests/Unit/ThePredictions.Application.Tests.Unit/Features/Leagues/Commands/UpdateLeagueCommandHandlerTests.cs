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

public class UpdateLeagueCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly UpdateLeagueCommandHandler _handler;

    public UpdateLeagueCommandHandlerTests()
    {
        _handler = new UpdateLeagueCommandHandler(_leagueRepository, _seasonRepository, _dateTimeProvider);
    }

    private Season CreateSeason(int id = 1) =>
        new(id: id, name: "2025/26",
            startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8),
            isActive: true, numberOfRounds: 38, competitionId: 1);

    private League CreateLeague(
        int id = 1,
        string administratorUserId = "admin-user",
        DateTime? entryDeadlineUtc = null,
        decimal price = 0m,
        IEnumerable<LeagueMember?>? members = null)
    {
        return new League(
            id: id, name: "Test League", seasonId: 1,
            administratorUserId: administratorUserId,
            entryCode: "ABC123",
            createdAtUtc: _dateTimeProvider.UtcNow.AddDays(-30),
            entryDeadlineUtc: entryDeadlineUtc ?? _dateTimeProvider.UtcNow.AddMonths(1),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: price, isFree: price == 0, hasPrizes: false,
            prizeFundOverride: null,
            members: members, prizeSettings: null);
    }

    [Fact]
    public async Task Handle_ShouldUpdateLeague_WhenUserIsAdministratorAndDeadlineNotPassed()
    {
        // Arrange
        var league = CreateLeague();
        var season = CreateSeason();
        var newDeadline = _dateTimeProvider.UtcNow.AddMonths(2).AddDays(-2);
        var command = new UpdateLeagueCommand(1, "Updated League", 5m, newDeadline, 4, 2, "admin-user");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFound()
    {
        // Arrange
        var command = new UpdateLeagueCommand(999, "Updated", 0m,
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, "admin-user");

        _leagueRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserIsNotAdministrator()
    {
        // Arrange
        var league = CreateLeague(administratorUserId: "admin-user");
        var command = new UpdateLeagueCommand(1, "Updated", 0m,
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, "other-user");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*league administrator*");
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenEntryDeadlineHasPassed()
    {
        // Arrange
        var league = CreateLeague(
            administratorUserId: "admin-user",
            entryDeadlineUtc: _dateTimeProvider.UtcNow.AddDays(-1));
        var command = new UpdateLeagueCommand(1, "Updated", 0m,
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, "admin-user");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entry deadline has passed*");
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenPriceChangedAndOtherMembersExist()
    {
        // Arrange
        var members = new List<LeagueMember?>
        {
            new(leagueId: 1, userId: "admin-user", status: LeagueMemberStatus.Approved,
                isAlertDismissed: false, isArchivedByUser: false, joinedAtUtc: _dateTimeProvider.UtcNow.AddDays(-20),
                approvedAtUtc: _dateTimeProvider.UtcNow.AddDays(-20), roundResults: null),
            new(leagueId: 1, userId: "other-user", status: LeagueMemberStatus.Approved,
                isAlertDismissed: false, isArchivedByUser: false, joinedAtUtc: _dateTimeProvider.UtcNow.AddDays(-10),
                approvedAtUtc: _dateTimeProvider.UtcNow.AddDays(-10), roundResults: null)
        };
        var league = CreateLeague(
            administratorUserId: "admin-user",
            price: 10m,
            members: members);
        var command = new UpdateLeagueCommand(1, "Updated", 20m,
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, "admin-user");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entry fee cannot be changed*");
    }

    [Fact]
    public async Task Handle_ShouldAllowPriceChange_WhenOnlyAdministratorIsMember()
    {
        // Arrange
        var members = new List<LeagueMember?>
        {
            new(leagueId: 1, userId: "admin-user", status: LeagueMemberStatus.Approved,
                isAlertDismissed: false, isArchivedByUser: false, joinedAtUtc: _dateTimeProvider.UtcNow.AddDays(-20),
                approvedAtUtc: _dateTimeProvider.UtcNow.AddDays(-20), roundResults: null)
        };
        var league = CreateLeague(
            administratorUserId: "admin-user",
            price: 10m,
            members: members);
        var season = CreateSeason();
        var command = new UpdateLeagueCommand(1, "Updated", 20m,
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, "admin-user");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenSeasonNotFound()
    {
        // Arrange
        var league = CreateLeague(administratorUserId: "admin-user");
        var command = new UpdateLeagueCommand(1, "Updated", 0m,
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, "admin-user");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((Season?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
