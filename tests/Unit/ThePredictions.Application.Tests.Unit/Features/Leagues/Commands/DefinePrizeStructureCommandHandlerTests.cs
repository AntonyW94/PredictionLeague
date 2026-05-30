using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class DefinePrizeStructureCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly DefinePrizeStructureCommandHandler _handler;

    private static readonly DateTime FixedNow = new(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc);

    public DefinePrizeStructureCommandHandlerTests()
    {
        var dateTimeProvider = new TestDateTimeProvider(FixedNow);
        _handler = new DefinePrizeStructureCommandHandler(_leagueRepository, _seasonRepository, _userManager, dateTimeProvider);
    }

    private static Season CreateSeason(int id = 1) =>
        new(id: id, name: "2025/26",
            startDateUtc: FixedNow.AddMonths(-2),
            endDateUtc: FixedNow.AddMonths(6),
            isActive: true, numberOfRounds: 38, competitionId: 1,
            passStandardPrice: null, passPremiumPrice: null);

    private static League CreateLeagueWithMembers(
        int id = 1,
        string administratorUserId = "admin-user",
        decimal price = 10m,
        int memberCount = 3,
        DateTime? entryDeadlineUtc = null)
    {
        var members = Enumerable.Range(0, memberCount)
            .Select(i => new LeagueMember(
                leagueId: id, userId: i == 0 ? administratorUserId : $"member-{i}",
                status: LeagueMemberStatus.Approved,
                isAlertDismissed: false, isArchivedByUser: false,
                joinedAtUtc: FixedNow.AddDays(-30 + i),
                approvedAtUtc: FixedNow.AddDays(-30 + i),
                roundResults: null))
            .Cast<LeagueMember?>()
            .ToList();

        return new League(
            id: id, name: "Test League", seasonId: 1,
            administratorUserId: administratorUserId,
            entryCode: "ABC123",
            createdAtUtc: FixedNow.AddDays(-60),
            entryDeadlineUtc: entryDeadlineUtc ?? FixedNow.AddDays(-1),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: price, isFree: false, hasPrizes: true,
            prizeFundOverride: null,
            members: members, prizeSettings: null);
    }

    [Fact]
    public async Task Handle_ShouldDefinePrizesAndUpdate_WhenValidRequestByAdministrator()
    {
        // Arrange
        var league = CreateLeagueWithMembers(price: 10m, memberCount: 3); // Total pot = 30
        var season = CreateSeason();
        var prizeSettings = new List<DefinePrizeSettingDto>
        {
            new() { PrizeType = PrizeType.Overall, Rank = 1, PrizeAmount = 30m, Multiplier = 1 }
        };
        var command = new DefinePrizeStructureCommand(1, "admin-user", prizeSettings);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _userManager.FindByIdAsync("admin-user").Returns(new ApplicationUser { Id = "admin-user" });
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Administrator).Returns(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFound()
    {
        // Arrange
        var command = new DefinePrizeStructureCommand(999, "admin-user", new List<DefinePrizeSettingDto>());

        _leagueRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserIsNotAdministratorOrSiteAdmin()
    {
        // Arrange
        var league = CreateLeagueWithMembers(administratorUserId: "admin-user");
        var season = CreateSeason();
        var command = new DefinePrizeStructureCommand(1, "other-user", new List<DefinePrizeSettingDto>());

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _userManager.FindByIdAsync("other-user").Returns(new ApplicationUser { Id = "other-user" });
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Administrator).Returns(false);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*league administrator*");
    }

    [Fact]
    public async Task Handle_ShouldAllowSiteAdmin_WhenNotLeagueAdministrator()
    {
        // Arrange
        var league = CreateLeagueWithMembers(price: 10m, memberCount: 3, administratorUserId: "league-admin");
        var season = CreateSeason();
        var prizeSettings = new List<DefinePrizeSettingDto>
        {
            new() { PrizeType = PrizeType.Overall, Rank = 1, PrizeAmount = 30m, Multiplier = 1 }
        };
        var command = new DefinePrizeStructureCommand(1, "site-admin", prizeSettings);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _userManager.FindByIdAsync("site-admin").Returns(new ApplicationUser { Id = "site-admin" });
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Administrator).Returns(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenEntryDeadlineHasNotPassed()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            price: 10m,
            memberCount: 3,
            entryDeadlineUtc: FixedNow.AddDays(1)); // Deadline in the future
        var season = CreateSeason();
        var command = new DefinePrizeStructureCommand(1, "admin-user", new List<DefinePrizeSettingDto>());

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _userManager.FindByIdAsync("admin-user").Returns(new ApplicationUser { Id = "admin-user" });
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Administrator).Returns(false);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entry deadline has passed*");
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenTotalPrizesDoNotMatchPot()
    {
        // Arrange
        var league = CreateLeagueWithMembers(price: 10m, memberCount: 3); // Total pot = 30
        var season = CreateSeason();
        var prizeSettings = new List<DefinePrizeSettingDto>
        {
            new() { PrizeType = PrizeType.Overall, Rank = 1, PrizeAmount = 20m, Multiplier = 1 } // Only 20, not 30
        };
        var command = new DefinePrizeStructureCommand(1, "admin-user", prizeSettings);

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _userManager.FindByIdAsync("admin-user").Returns(new ApplicationUser { Id = "admin-user" });
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Administrator).Returns(false);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*total allocated prize money must equal*");
    }
}
