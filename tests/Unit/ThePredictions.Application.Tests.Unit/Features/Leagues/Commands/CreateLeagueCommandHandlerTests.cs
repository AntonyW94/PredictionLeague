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

public class CreateLeagueCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly CreateLeagueCommandHandler _handler;

    public CreateLeagueCommandHandlerTests()
    {
        _handler = new CreateLeagueCommandHandler(_leagueRepository, _seasonRepository, _dateTimeProvider);
    }

    private Season CreateSeason(int id = 1) =>
        new(id: id, name: "2025/26",
            startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8),
            isActive: true, numberOfRounds: 38, competitionId: 1);

    [Fact]
    public async Task Handle_ShouldReturnLeagueDto_WhenRequestIsValid()
    {
        // Arrange
        var season = CreateSeason();
        var entryDeadlineUtc = _dateTimeProvider.UtcNow.AddMonths(1);
        var command = new CreateLeagueCommand("Test League", 1, 10m, "user-1", entryDeadlineUtc, 3, 1);

        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _leagueRepository.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((League?)null);
        _leagueRepository.CreateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var league = callInfo.ArgAt<League>(0);
                return new League(
                    id: 42, name: league.Name, seasonId: league.SeasonId,
                    administratorUserId: league.AdministratorUserId,
                    entryCode: league.EntryCode, createdAtUtc: _dateTimeProvider.UtcNow,
                    entryDeadlineUtc: league.EntryDeadlineUtc,
                    pointsForExactScore: league.PointsForExactScore,
                    pointsForCorrectResult: league.PointsForCorrectResult,
                    price: league.Price, isFree: league.IsFree, hasPrizes: false,
                    prizeFundOverride: null,
                    members: null, prizeSettings: null);
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(42);
        result.Name.Should().Be("Test League");
        result.SeasonName.Should().Be("2025/26");
        result.Price.Should().Be(10m);
        result.PointsForExactScore.Should().Be(3);
        result.PointsForCorrectResult.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenSeasonNotFound()
    {
        // Arrange
        var command = new CreateLeagueCommand("Test League", 999, 10m, "user-1",
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1);

        _seasonRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((Season?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldGenerateUniqueEntryCode_WhenFirstCodeAlreadyExists()
    {
        // Arrange
        var season = CreateSeason();
        var entryDeadlineUtc = _dateTimeProvider.UtcNow.AddMonths(1);
        var command = new CreateLeagueCommand("Test League", 1, 0m, "user-1", entryDeadlineUtc, 3, 1);

        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);

        // First call returns an existing league (code collision), second call returns null (unique code)
        _leagueRepository.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                new League(id: 1, name: "Existing", seasonId: 1, administratorUserId: "other-user",
                    entryCode: "ABC123", createdAtUtc: _dateTimeProvider.UtcNow,
                    entryDeadlineUtc: entryDeadlineUtc, pointsForExactScore: 3,
                    pointsForCorrectResult: 1, price: 0, isFree: true, hasPrizes: false,
                    prizeFundOverride: null, members: null, prizeSettings: null),
                (League?)null);

        _leagueRepository.CreateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<League>(0));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - GetByEntryCodeAsync should have been called at least twice (first collision, then unique)
        await _leagueRepository.Received(2).GetByEntryCodeAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetEntryCodeOnLeague_WhenCreating()
    {
        // Arrange
        var season = CreateSeason();
        var entryDeadlineUtc = _dateTimeProvider.UtcNow.AddMonths(1);
        var command = new CreateLeagueCommand("Test League", 1, 0m, "user-1", entryDeadlineUtc, 3, 1);

        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _leagueRepository.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((League?)null);
        _leagueRepository.CreateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<League>(0));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).CreateAsync(
            Arg.Is<League>(l => !string.IsNullOrEmpty(l.EntryCode)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDtoWithMemberCountOfOne_WhenLeagueCreated()
    {
        // Arrange
        var season = CreateSeason();
        var entryDeadlineUtc = _dateTimeProvider.UtcNow.AddMonths(1);
        var command = new CreateLeagueCommand("Test League", 1, 0m, "user-1", entryDeadlineUtc, 3, 1);

        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _leagueRepository.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((League?)null);
        _leagueRepository.CreateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var league = callInfo.ArgAt<League>(0);
                return new League(
                    id: 1, name: league.Name, seasonId: 1,
                    administratorUserId: "user-1", entryCode: league.EntryCode,
                    createdAtUtc: _dateTimeProvider.UtcNow,
                    entryDeadlineUtc: entryDeadlineUtc,
                    pointsForExactScore: 3, pointsForCorrectResult: 1,
                    price: 0, isFree: true, hasPrizes: false,
                    prizeFundOverride: null, members: null, prizeSettings: null);
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.MemberCount.Should().Be(1);
    }
}
