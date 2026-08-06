using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Strategies;

/// <summary>
/// Pays the monthly prize, but only on the last round of the month - firing a round early would
/// pay out on an unfinished month's standings.
/// </summary>
public class MonthlyPrizeStrategyTests
{
    private const int May = 5;

    private readonly PrizeStrategyScenario _scenario = new();
    private readonly MonthlyPrizeStrategy _strategy;

    public MonthlyPrizeStrategyTests() =>
        _strategy = new MonthlyPrizeStrategy(
            _scenario.Winnings, _scenario.Rounds, _scenario.Leagues,
            new TestDateTimeProvider(PrizeStrategyScenario.FixedNow));

    private Task AwardAsync() => _strategy.AwardPrizes(PrizeStrategyScenario.Command, CancellationToken.None);

    private void GivenLastRoundOfMonth(bool isLast = true) =>
        _scenario.Rounds.IsLastRoundOfMonthAsync(PrizeStrategyScenario.RoundId, PrizeStrategyScenario.SeasonId, Arg.Any<CancellationToken>())
            .Returns(isLast);

    private void GivenRoundsInMonth(params int[] roundIds) =>
        _scenario.Rounds.GetRoundsIdsForMonthAsync(May, PrizeStrategyScenario.SeasonId, Arg.Any<CancellationToken>())
            .Returns(roundIds.ToList());

    [Fact]
    public void PrizeType_ShouldBeMonthly() => _strategy.PrizeType.Should().Be(PrizeType.Monthly);

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheRoundIsMissing()
    {
        _scenario.GivenNoRound();

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
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
    public async Task AwardPrizes_ShouldWait_WhenTheMonthIsNotFinished()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Monthly, 60m)], ("user-1", 10, 0));
        GivenLastRoundOfMonth(false);

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
        await _scenario.Winnings.DidNotReceiveWithAnyArgs().DeleteWinningsForMonthAsync(default, default, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheLeagueHasNoMonthlyPrize()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Round, 60m)], ("user-1", 10, 0));
        GivenLastRoundOfMonth();

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldClearThePreviousMonthlyPayout_BeforeAwarding()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Monthly, 60m)], ("user-1", 10, 0));
        GivenLastRoundOfMonth();
        GivenRoundsInMonth(PrizeStrategyScenario.RoundId);
        _scenario.CaptureWinnings();

        await AwardAsync();

        await _scenario.Winnings.Received(1).DeleteWinningsForMonthAsync(
            PrizeStrategyScenario.LeagueId, May, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AwardPrizes_ShouldPayTheMonthsWinnerAndStampTheMonth()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Monthly, 60m)],
            ("user-1", 20, 0), ("user-2", 5, 0));
        GivenLastRoundOfMonth();
        GivenRoundsInMonth(PrizeStrategyScenario.RoundId);
        _scenario.CaptureWinnings();

        await AwardAsync();

        var winning = _scenario.CapturedWinnings.Should().ContainSingle().Subject;
        winning.UserId.Should().Be("user-1");
        winning.Amount.Should().Be(60m);
        winning.Month.Should().Be(May);
        winning.RoundNumber.Should().BeNull();
    }

    [Fact]
    public async Task AwardPrizes_ShouldSplitBetweenJointWinners()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Monthly, 60m)],
            ("user-1", 20, 0), ("user-2", 20, 0));
        GivenLastRoundOfMonth();
        GivenRoundsInMonth(PrizeStrategyScenario.RoundId);
        _scenario.CaptureWinnings();

        await AwardAsync();

        _scenario.CapturedWinnings.Should().HaveCount(2);
        _scenario.CapturedWinnings.Sum(w => w.Amount).Should().Be(60m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldClearButNotPay_WhenTheMonthHasNoWinner()
    {
        _scenario.GivenRound();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Monthly, 60m)], ("user-1", 10, 0));
        GivenLastRoundOfMonth();
        GivenRoundsInMonth();

        await AwardAsync();

        await _scenario.Winnings.Received(1).DeleteWinningsForMonthAsync(
            PrizeStrategyScenario.LeagueId, May, Arg.Any<CancellationToken>());
        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldTakeTheMonthFromTheRoundStartNotToday()
    {
        // The job can run after midnight on the 1st; the prize still belongs to the month the
        // round was played in.
        _scenario.GivenRound(startDateUtc: new DateTime(2026, 5, 30, 15, 0, 0, DateTimeKind.Utc));
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Monthly, 60m)], ("user-1", 20, 0));
        GivenLastRoundOfMonth();
        GivenRoundsInMonth(PrizeStrategyScenario.RoundId);
        _scenario.CaptureWinnings();

        await AwardAsync();

        _scenario.CapturedWinnings.Should().ContainSingle().Which.Month.Should().Be(May);
    }
}
