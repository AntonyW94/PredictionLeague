using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.Boosts.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Boosts.Commands;

public class SetLeagueBoostRulesCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ILeagueBoostRuleRepository _boostRuleRepository = Substitute.For<ILeagueBoostRuleRepository>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));
    private readonly SetLeagueBoostRulesCommandHandler _handler;

    public SetLeagueBoostRulesCommandHandlerTests()
    {
        _handler = new SetLeagueBoostRulesCommandHandler(_leagueRepository, _boostRuleRepository, _userManager, Substitute.For<ILogger<SetLeagueBoostRulesCommandHandler>>());
    }

    private League CreateLeague(string adminUserId = "admin-user") =>
        new(1, "Test League", 1, adminUserId, "ABC123", _dateTimeProvider.UtcNow, _dateTimeProvider.UtcNow.AddMonths(1),
            3, 1, 10m, false, false, null, members: null, prizeSettings: null);

    private List<LeagueBoostSelectionDto> Selections() => new()
    {
        new LeagueBoostSelectionDto { BoostCode = "DOUBLE_UP", IsEnabled = true, TotalUsesPerSeason = 3 }
    };

    private void ArrangeUser(bool isSiteAdmin)
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(new ApplicationUser { Id = "u" });
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Administrator).Returns(isSiteAdmin);
    }

    [Fact]
    public async Task Handle_ShouldSetRules_WhenLeagueAdminAndUnset()
    {
        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateLeague());
        _boostRuleRepository.HasRulesAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        ArrangeUser(isSiteAdmin: false);

        await _handler.Handle(new SetLeagueBoostRulesCommand(1, "admin-user", Selections()), CancellationToken.None);

        await _boostRuleRepository.Received(1).SetRulesAsync(1, Arg.Any<IReadOnlyList<LeagueBoostSelectionDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenNonAdminAndUnset()
    {
        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateLeague(adminUserId: "someone"));
        _boostRuleRepository.HasRulesAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        ArrangeUser(isSiteAdmin: false);

        var act = () => _handler.Handle(new SetLeagueBoostRulesCommand(1, "intruder", Selections()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenAlreadySetAndNotSiteAdmin()
    {
        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateLeague());
        _boostRuleRepository.HasRulesAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        ArrangeUser(isSiteAdmin: false);

        var act = () => _handler.Handle(new SetLeagueBoostRulesCommand(1, "admin-user", Selections()), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task Handle_ShouldOverride_WhenAlreadySetAndSiteAdmin()
    {
        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateLeague());
        _boostRuleRepository.HasRulesAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        ArrangeUser(isSiteAdmin: true);

        await _handler.Handle(new SetLeagueBoostRulesCommand(1, "site-admin", Selections()), CancellationToken.None);

        await _boostRuleRepository.Received(1).SetRulesAsync(1, Arg.Any<IReadOnlyList<LeagueBoostSelectionDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenLeagueMissing()
    {
        _leagueRepository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((League?)null);

        var act = () => _handler.Handle(new SetLeagueBoostRulesCommand(99, "admin-user", Selections()), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
