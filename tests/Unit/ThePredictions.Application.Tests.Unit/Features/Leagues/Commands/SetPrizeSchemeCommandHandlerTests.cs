using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class SetPrizeSchemeCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly ICompetitionRepository _competitionRepository = Substitute.For<ICompetitionRepository>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));
    private readonly SetPrizeSchemeCommandHandler _handler;

    public SetPrizeSchemeCommandHandlerTests()
    {
        _handler = new SetPrizeSchemeCommandHandler(_leagueRepository, _seasonRepository, _competitionRepository, _userManager, _dateTimeProvider, Substitute.For<ILogger<SetPrizeSchemeCommandHandler>>());
    }

    private Season CreateSeason() =>
        new(1, "2026/27", _dateTimeProvider.UtcNow.AddMonths(2), _dateTimeProvider.UtcNow.AddMonths(8), true, 38, 1, null, null);

    private Competition CreateCompetition(CompetitionType type = CompetitionType.League) =>
        new(1, "EPL", "Premier League", type, null, null, null, _dateTimeProvider.UtcNow);

    private League CreateLeague(string adminUserId = "admin-user", decimal price = 10m, LeaguePrizeScheme? scheme = null) =>
        new(1, "Test League", 1, adminUserId, "ABC123", _dateTimeProvider.UtcNow, _dateTimeProvider.UtcNow.AddMonths(1),
            3, 1, price, false, scheme is not null, null, members: null, prizeSettings: null, prizeScheme: scheme);

    private LeaguePrizeScheme ExistingScheme() =>
        LeaguePrizeScheme.Create(10, new[] { LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 10) }, "admin-user", false, _dateTimeProvider);

    private static PrizeSchemeRequest Request() => new()
    {
        Categories = new List<PrizeSchemeCategoryRequest>
        {
            new() { Category = PrizeType.Overall, PerEntryPounds = 7 },
            new() { Category = PrizeType.MostExactScores, PerEntryPounds = 3 }
        }
    };

    private void Arrange(League league, bool isSiteAdmin)
    {
        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSeason());
        _competitionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateCompetition());
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(new ApplicationUser { Id = "u" });
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Administrator).Returns(isSiteAdmin);
    }

    [Fact]
    public async Task Handle_ShouldSaveScheme_WhenLeagueAdminSetsUnsetScheme()
    {
        var league = CreateLeague();
        Arrange(league, isSiteAdmin: false);

        await _handler.Handle(new SetPrizeSchemeCommand(1, "admin-user", Request()), CancellationToken.None);

        await _leagueRepository.Received(1).SavePrizeSchemeAsync(1, Arg.Any<LeaguePrizeScheme>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenNonAdminSetsUnsetScheme()
    {
        var league = CreateLeague(adminUserId: "someone-else");
        Arrange(league, isSiteAdmin: false);

        var act = () => _handler.Handle(new SetPrizeSchemeCommand(1, "intruder", Request()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _leagueRepository.DidNotReceiveWithAnyArgs().SavePrizeSchemeAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenNonSiteAdminChangesSetScheme()
    {
        var league = CreateLeague(scheme: ExistingScheme());
        Arrange(league, isSiteAdmin: false);

        var act = () => _handler.Handle(new SetPrizeSchemeCommand(1, "admin-user", Request()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been set*");
    }

    [Fact]
    public async Task Handle_ShouldOverrideScheme_WhenSiteAdminChangesSetScheme()
    {
        var league = CreateLeague(scheme: ExistingScheme());
        Arrange(league, isSiteAdmin: true);

        await _handler.Handle(new SetPrizeSchemeCommand(1, "site-admin", Request()), CancellationToken.None);

        await _leagueRepository.Received(1).SavePrizeSchemeAsync(1, Arg.Any<LeaguePrizeScheme>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenLeagueMissing()
    {
        _leagueRepository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((League?)null);

        var act = () => _handler.Handle(new SetPrizeSchemeCommand(99, "admin-user", Request()), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenEntryFeeHasPence()
    {
        var league = CreateLeague(price: 10.50m);
        Arrange(league, isSiteAdmin: false);

        var act = () => _handler.Handle(new SetPrizeSchemeCommand(1, "admin-user", Request()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*whole number of pounds*");
    }
}
