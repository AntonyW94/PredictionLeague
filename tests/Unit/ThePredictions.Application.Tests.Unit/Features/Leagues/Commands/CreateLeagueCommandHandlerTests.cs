using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
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
    private readonly ICompetitionRepository _competitionRepository = Substitute.For<ICompetitionRepository>();
    private readonly ISeasonAccessService _seasonAccessService = Substitute.For<ISeasonAccessService>();
    private readonly IFieldEncryptionService _fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
    private readonly IBadgeAwardService _badgeAwardService = Substitute.For<IBadgeAwardService>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly CreateLeagueCommandHandler _handler;

    public CreateLeagueCommandHandlerTests()
    {
        _handler = new CreateLeagueCommandHandler(_leagueRepository, _seasonRepository, _competitionRepository, _seasonAccessService, _fieldEncryptionService, _badgeAwardService, _dateTimeProvider);
    }

    private Season CreateSeason(int id = 1) =>
        new(id: id, name: "2025/26",
            startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8),
            isActive: true, numberOfRounds: 38, competitionId: 1,
            passStandardPrice: null, passPremiumPrice: null);

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
    public async Task Handle_ShouldEncryptBankDetails_WhenProvided()
    {
        // Arrange
        var season = CreateSeason();
        var entryDeadlineUtc = _dateTimeProvider.UtcNow.AddMonths(1);
        var command = new CreateLeagueCommand(
            "Test League", 1, 10m, "user-1", entryDeadlineUtc, 3, 1,
            BankAccountName: "Mr A Willson", BankSortCode: "12-34-56",
            BankAccountNumber: "12345678", PaymentReferenceTemplate: "WC-{Name}");

        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _leagueRepository.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((League?)null);
        _fieldEncryptionService.Encrypt(Arg.Any<string?>())
            .Returns(callInfo => callInfo.Arg<string?>() is { } value ? $"enc:{value}" : null);

        League? captured = null;
        _leagueRepository.CreateAsync(Arg.Do<League>(l => captured = l), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<League>());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.BankAccountName.Should().Be("enc:Mr A Willson");
        captured.BankSortCode.Should().Be("enc:12-34-56");
        captured.BankAccountNumber.Should().Be("enc:12345678");
        captured.PaymentReferenceTemplate.Should().Be("WC-{Name}");
    }

    [Fact]
    public async Task Handle_ShouldAttachPrizeScheme_WhenSchemeProvided()
    {
        // Arrange
        var season = CreateSeason();
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _competitionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Competition(1, "EPL", "Premier League", CompetitionType.League, null, null, null, _dateTimeProvider.UtcNow));
        _leagueRepository.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((League?)null);

        League? captured = null;
        _leagueRepository.CreateAsync(Arg.Do<League>(l => captured = l), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<League>());

        var scheme = new ThePredictions.Contracts.Prizes.PrizeSchemeRequest
        {
            Categories = new List<ThePredictions.Contracts.Prizes.PrizeSchemeCategoryRequest>
            {
                new() { Category = PrizeType.Overall, PerEntryPounds = 7 },
                new() { Category = PrizeType.Round, PerEntryPounds = 3 }
            }
        };

        var command = new CreateLeagueCommand("Test League", 1, 10m, "user-1", _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, PrizeScheme: scheme);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.PrizeScheme.Should().NotBeNull();
        captured.PrizeScheme!.Entries.Should().HaveCount(2);
        captured.HasPrizes.Should().BeTrue();
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

    [Fact]
    public async Task Handle_ShouldDescribeALeagueWithNoEntryCodeAsPublic()
    {
        // A league with no code is open to anyone, and the screen shows the word "Public" where a
        // private league would show its join code.
        var season = CreateSeason();
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _leagueRepository.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((League?)null);
        _leagueRepository.CreateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            var league = callInfo.ArgAt<League>(0);
            return new League(
                id: 42, name: league.Name, seasonId: league.SeasonId,
                administratorUserId: league.AdministratorUserId,
                entryCode: null, createdAtUtc: _dateTimeProvider.UtcNow,
                entryDeadlineUtc: league.EntryDeadlineUtc,
                pointsForExactScore: league.PointsForExactScore,
                pointsForCorrectResult: league.PointsForCorrectResult,
                price: league.Price, isFree: league.IsFree, hasPrizes: false,
                prizeFundOverride: null, members: null, prizeSettings: null);
        });

        var result = await _handler.Handle(
            new CreateLeagueCommand("Test League", 1, 10m, "user-1", _dateTimeProvider.UtcNow.AddMonths(1), 3, 1),
            CancellationToken.None);

        result.EntryCode.Should().Be("Public");
    }
}
