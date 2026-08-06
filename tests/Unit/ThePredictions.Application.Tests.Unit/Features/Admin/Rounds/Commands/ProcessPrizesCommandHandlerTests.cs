using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// Dispatches a league's frozen prize settings to the strategy that knows how to pay each one. Also
/// the safety net that freezes a scheme the scheduled job has not got to yet - without it, a league
/// whose deadline passed minutes before the last match would pay nobody.
/// </summary>
public class ProcessPrizesCommandHandlerTests
{
    private const int LeagueId = 5;
    private const int RoundId = 100;

    private static readonly DateTime FixedNow = new(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IPrizeSchemeFreezeService _freezeService = Substitute.For<IPrizeSchemeFreezeService>();

    private readonly ProcessPrizesCommand _command = new() { LeagueId = LeagueId, RoundId = RoundId };

    private static IPrizeStrategy Strategy(PrizeType prizeType)
    {
        var strategy = Substitute.For<IPrizeStrategy>();
        strategy.PrizeType.Returns(prizeType);
        return strategy;
    }

    private ProcessPrizesCommandHandler BuildHandler(params IPrizeStrategy[] strategies) =>
        new(strategies, _leagueRepository, _freezeService);

    private League GivenLeague(params PrizeType[] prizeTypes)
    {
        var settings = prizeTypes.Select((t, i) => LeaguePrizeSetting.Create(LeagueId, t, 1, 10m * (i + 1))).ToList();

        var league = new League(
            id: LeagueId, name: "Test League", seasonId: 1, administratorUserId: "admin",
            entryCode: "ABC123", createdAtUtc: FixedNow.AddDays(-90), entryDeadlineUtc: FixedNow.AddDays(-60),
            pointsForExactScore: 3, pointsForCorrectResult: 1, price: 100m, isFree: false,
            hasPrizes: true, prizeFundOverride: null, members: [], prizeSettings: settings);

        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(league);
        return league;
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheLeagueDoesNotExist()
    {
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((League?)null);
        var strategy = Strategy(PrizeType.Round);

        await BuildHandler(strategy).Handle(_command, CancellationToken.None);

        await strategy.DidNotReceiveWithAnyArgs().AwardPrizes(default!, CancellationToken.None);
        await _freezeService.DidNotReceiveWithAnyArgs().TryFreezeAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldFreezeTheSchemeLazily_WhenTheLeagueHasNoSettingsYet()
    {
        GivenLeague();
        var strategy = Strategy(PrizeType.Round);

        await BuildHandler(strategy).Handle(_command, CancellationToken.None);

        await _freezeService.Received(1).TryFreezeAsync(Arg.Any<League>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStop_WhenFreezingProducesNoPrizes()
    {
        // A free league with no scheme has nothing to pay, and that is not an error.
        GivenLeague();
        var strategy = Strategy(PrizeType.Round);

        await BuildHandler(strategy).Handle(_command, CancellationToken.None);

        await strategy.DidNotReceiveWithAnyArgs().AwardPrizes(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldNotFreezeAgain_WhenTheSchemeIsAlreadyFrozen()
    {
        GivenLeague(PrizeType.Round);
        var strategy = Strategy(PrizeType.Round);

        await BuildHandler(strategy).Handle(_command, CancellationToken.None);

        await _freezeService.DidNotReceiveWithAnyArgs().TryFreezeAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRunTheStrategyMatchingEachPrizeSetting()
    {
        GivenLeague(PrizeType.Round, PrizeType.Overall);
        var round = Strategy(PrizeType.Round);
        var overall = Strategy(PrizeType.Overall);
        var monthly = Strategy(PrizeType.Monthly);

        await BuildHandler(round, overall, monthly).Handle(_command, CancellationToken.None);

        await round.Received(1).AwardPrizes(_command, Arg.Any<CancellationToken>());
        await overall.Received(1).AwardPrizes(_command, Arg.Any<CancellationToken>());
        await monthly.DidNotReceiveWithAnyArgs().AwardPrizes(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSkipAPrizeTypeWithNoRegisteredStrategy()
    {
        // A prize type can be configured before its strategy ships; that must not stop the others.
        GivenLeague(PrizeType.Round, PrizeType.Stages);
        var round = Strategy(PrizeType.Round);

        var act = () => BuildHandler(round).Handle(_command, CancellationToken.None);

        await act.Should().NotThrowAsync();
        await round.Received(1).AwardPrizes(_command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnit()
    {
        GivenLeague(PrizeType.Round);

        var result = await BuildHandler(Strategy(PrizeType.Round)).Handle(_command, CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
    }
}
