using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Strategies;

/// <summary>
/// Pays the season-long "most exact scores" prize, and only once the season is actually over -
/// this one is decided on exact-score counts rather than points.
/// </summary>
public class MostExactScoresPrizeStrategyTests
{
    private readonly PrizeStrategyScenario _scenario = new();
    private readonly MostExactScoresPrizeStrategy _strategy;

    public MostExactScoresPrizeStrategyTests() =>
        _strategy = new MostExactScoresPrizeStrategy(
            _scenario.Winnings, _scenario.Rounds, _scenario.Leagues,
            new TestDateTimeProvider(PrizeStrategyScenario.FixedNow));

    private Task AwardAsync() => _strategy.AwardPrizes(PrizeStrategyScenario.Command, CancellationToken.None);

    private void GivenLastRoundOfSeason(bool isLast = true) =>
        _scenario.Rounds.IsLastRoundOfSeasonAsync(PrizeStrategyScenario.RoundId, PrizeStrategyScenario.SeasonId, Arg.Any<CancellationToken>())
            .Returns(isLast);

    [Fact]
    public void PrizeType_ShouldBeMostExactScores() => _strategy.PrizeType.Should().Be(PrizeType.MostExactScores);

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheRoundIsMissing()
    {
        _scenario.GivenNoRound();

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldWait_WhenTheSeasonIsNotOver()
    {
        _scenario.GivenRound();
        GivenLastRoundOfSeason(false);

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
        await _scenario.Leagues.DidNotReceiveWithAnyArgs().GetByIdWithAllDataAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheLeagueIsMissing()
    {
        _scenario.GivenRound();
        GivenLastRoundOfSeason();
        _scenario.GivenNoLeague();

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheLeagueHasNoExactScoresPrize()
    {
        _scenario.GivenRound();
        GivenLastRoundOfSeason();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.Overall, 80m)], ("user-1", 10, 5));

        await AwardAsync();

        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldPayThePlayerWithTheMostExactScores()
    {
        // Decided on exact scores, not points - the player with fewer points can still win it.
        _scenario.GivenRound();
        GivenLastRoundOfSeason();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.MostExactScores, 80m)],
            ("user-1", 100, 2), ("user-2", 40, 7));
        _scenario.CaptureWinnings();

        await AwardAsync();

        var winning = _scenario.CapturedWinnings.Should().ContainSingle().Subject;
        winning.UserId.Should().Be("user-2");
        winning.Amount.Should().Be(80m);
        winning.RoundNumber.Should().BeNull();
        winning.Month.Should().BeNull();
    }

    [Fact]
    public async Task AwardPrizes_ShouldSplitBetweenPlayersLevelOnExactScores()
    {
        _scenario.GivenRound();
        GivenLastRoundOfSeason();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.MostExactScores, 80m)],
            ("user-1", 100, 7), ("user-2", 40, 7));
        _scenario.CaptureWinnings();

        await AwardAsync();

        _scenario.CapturedWinnings.Should().HaveCount(2);
        _scenario.CapturedWinnings.Sum(w => w.Amount).Should().Be(80m);
        _scenario.CapturedWinnings.Should().OnlyContain(w => w.Amount == 40m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldClearThePreviousPayout_BeforeAwarding()
    {
        _scenario.GivenRound();
        GivenLastRoundOfSeason();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.MostExactScores, 80m)], ("user-1", 10, 3));
        _scenario.CaptureWinnings();

        await AwardAsync();

        await _scenario.Winnings.Received(1).DeleteWinningsForMostExactScoresAsync(
            PrizeStrategyScenario.LeagueId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AwardPrizes_ShouldClearButNotPay_WhenNobodyHasAnExactScore()
    {
        _scenario.GivenRound();
        GivenLastRoundOfSeason();
        _scenario.GivenLeague([PrizeStrategyScenario.PrizeSetting(11, PrizeType.MostExactScores, 80m)], ("user-1", 10, 0));

        await AwardAsync();

        await _scenario.Winnings.Received(1).DeleteWinningsForMostExactScoresAsync(
            PrizeStrategyScenario.LeagueId, Arg.Any<CancellationToken>());
        await _scenario.Winnings.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }
}
