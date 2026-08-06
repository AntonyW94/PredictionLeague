using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Strategies;

/// <summary>
/// Pays the per-round prize. It runs after every round, so the guards matter as much as the maths:
/// paying twice, or paying a league that has no round prize, both cost real money.
/// </summary>
public class RoundPrizeStrategyTests
{
    private readonly PrizeStrategyScenario _scenario = new();
    private readonly RoundPrizeStrategy _strategy;

    public RoundPrizeStrategyTests() =>
        _strategy = new RoundPrizeStrategy(
            _scenario.Winnings, _scenario.Rounds, _scenario.Leagues,
            new TestDateTimeProvider(PrizeStrategyScenario.FixedNow));

    private Task AwardAsync() => _strategy.AwardPrizes(PrizeStrategyScenario.Command, CancellationToken.None);

    [Fact]
    public void PrizeType_ShouldBeRound() => _strategy.PrizeType.Should().Be(PrizeType.Round);

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheRoundIsMissing()
    {
        _scenario.GivenNoRound();

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
        await _scenario.Winnings.DidNotReceiveWithAnyArgs().DeleteWinningsForRoundAsync(default, default, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheLeagueIsMissing()
    {
        _scenario.GivenRound();
        _scenario.GivenNoLeague();

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheLeagueHasNoRoundPrize()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Overall, 100m)], ("user-1", 10, 0));

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
        await _scenario.Winnings.DidNotReceiveWithAnyArgs().DeleteWinningsForRoundAsync(default, default, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldClearAnyPreviousPayout_BeforeAwarding()
    {
        // Rounds can be re-scored after a correction, so the previous payout has to go first or
        // the winner is paid twice.
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Round, 50m)], ("user-1", 10, 0));
        _scenario.CaptureWinnings();

        await AwardAsync();

        await _scenario.Winnings.Received(1).DeleteWinningsForRoundAsync(
            PrizeStrategyScenario.LeagueId, PrizeStrategyScenario.RoundNumber, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AwardPrizes_ShouldPayTheOutrightWinnerInFull()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Round, 50m)],
            ("user-1", 12, 0), ("user-2", 8, 0));
        _scenario.CaptureWinnings();

        await AwardAsync();

        _scenario.CapturedWinnings.Should().ContainSingle();
        var winning = _scenario.CapturedWinnings[0];
        winning.UserId.Should().Be("user-1");
        winning.Amount.Should().Be(50m);
        winning.LeaguePrizeSettingId.Should().Be(11);
        winning.RoundNumber.Should().Be(PrizeStrategyScenario.RoundNumber);
        winning.Month.Should().BeNull();
    }

    [Fact]
    public async Task AwardPrizes_ShouldSplitThePrizeBetweenJointWinners()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Round, 50m)],
            ("user-1", 12, 0), ("user-2", 12, 0));
        _scenario.CaptureWinnings();

        await AwardAsync();

        _scenario.CapturedWinnings.Should().HaveCount(2);
        _scenario.CapturedWinnings.Sum(w => w.Amount).Should().Be(50m);
        _scenario.CapturedWinnings.Should().OnlyContain(w => w.Amount == 25m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldStillPayTheWholePrize_WhenItDoesNotDivideEvenly()
    {
        // Three-way split of £50: nobody may be short-changed and the league must not overpay.
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Round, 50m)],
            ("user-1", 12, 0), ("user-2", 12, 0), ("user-3", 12, 0));
        _scenario.CaptureWinnings();

        await AwardAsync();

        _scenario.CapturedWinnings.Should().HaveCount(3);
        _scenario.CapturedWinnings.Sum(w => w.Amount).Should().Be(50m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldClearButNotPay_WhenNobodyScored()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Round, 50m)]);
        _scenario.CaptureWinnings();

        await AwardAsync();

        await _scenario.Winnings.Received(1).DeleteWinningsForRoundAsync(
            PrizeStrategyScenario.LeagueId, PrizeStrategyScenario.RoundNumber, Arg.Any<CancellationToken>());
        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }
}
