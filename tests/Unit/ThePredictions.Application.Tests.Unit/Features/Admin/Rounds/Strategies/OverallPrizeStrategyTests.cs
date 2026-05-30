using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Strategies;

public class OverallPrizeStrategyTests
{
    private readonly IWinningsRepository _winningsRepository = Substitute.For<IWinningsRepository>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly OverallPrizeStrategy _strategy;

    private static readonly DateTime FixedNow = new(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);

    private const int LeagueId = 1;
    private const int RoundId = 100;
    private const int SeasonId = 1;

    public OverallPrizeStrategyTests()
    {
        _strategy = new OverallPrizeStrategy(
            _winningsRepository,
            _roundRepository,
            _leagueRepository,
            new TestDateTimeProvider(FixedNow));
    }

    [Fact]
    public async Task AwardPrizes_ShouldReturnEarly_WhenRoundNotFound()
    {
        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns((Round?)null);

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs()
            .AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldReturnEarly_WhenNotLastRoundOfSeason()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs()
            .AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldReturnEarly_WhenLeagueNotFound()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns((League?)null);

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs()
            .AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldReturnEarly_WhenNoOverallPrizeSettings()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        var league = CreateLeague(
            prizeSettings: new List<LeaguePrizeSetting>
            {
                CreatePrizeSetting(99, PrizeType.Round, 1, 10m)
            },
            members: new List<(string UserId, int Points)>
            {
                ("user-1", 100)
            });
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(league);

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs()
            .AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldNotAddWinnings_WhenLeagueHasNoMembers()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        var league = CreateLeague(
            prizeSettings: new List<LeaguePrizeSetting>
            {
                CreatePrizeSetting(1, PrizeType.Overall, 1, 100m)
            },
            members: new List<(string UserId, int Points)>());
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(league);

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        await _winningsRepository.Received(1).DeleteWinningsForOverallAsync(LeagueId, Arg.Any<CancellationToken>());
        await _winningsRepository.DidNotReceiveWithAnyArgs()
            .AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldAwardEachRankSeparately_WhenNoJointWinners()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        var firstPrize = CreatePrizeSetting(11, PrizeType.Overall, 1, 220m);
        var secondPrize = CreatePrizeSetting(12, PrizeType.Overall, 2, 120m);
        var thirdPrize = CreatePrizeSetting(13, PrizeType.Overall, 3, 60m);
        var league = CreateLeague(
            prizeSettings: new List<LeaguePrizeSetting> { firstPrize, secondPrize, thirdPrize },
            members: new List<(string UserId, int Points)>
            {
                ("user-1", 100),
                ("user-2", 80),
                ("user-3", 60)
            });
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(league);

        IEnumerable<Winning>? capturedWinnings = null;
        await _winningsRepository.AddWinningsAsync(
            Arg.Do<IEnumerable<Winning>>(w => capturedWinnings = w.ToList()),
            Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        capturedWinnings.Should().NotBeNull();
        var winnings = capturedWinnings!.ToList();
        winnings.Should().HaveCount(3);
        winnings.Single(w => w.UserId == "user-1").Amount.Should().Be(220m);
        winnings.Single(w => w.UserId == "user-1").LeaguePrizeSettingId.Should().Be(11);
        winnings.Single(w => w.UserId == "user-2").Amount.Should().Be(120m);
        winnings.Single(w => w.UserId == "user-2").LeaguePrizeSettingId.Should().Be(12);
        winnings.Single(w => w.UserId == "user-3").Amount.Should().Be(60m);
        winnings.Single(w => w.UserId == "user-3").LeaguePrizeSettingId.Should().Be(13);
    }

    [Fact]
    public async Task AwardPrizes_ShouldPoolThirdPlaceIntoJointSecond_WhenTwoUsersAreJointSecond()
    {
        // Regression: Premier League 2025/26 McKenzie's League.
        // 1st, then 2 joint 2nd. Each joint 2nd should get (120 + 60) / 2 = 90, not 60.
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        var firstPrize = CreatePrizeSetting(11, PrizeType.Overall, 1, 220m);
        var secondPrize = CreatePrizeSetting(12, PrizeType.Overall, 2, 120m);
        var thirdPrize = CreatePrizeSetting(13, PrizeType.Overall, 3, 60m);
        var league = CreateLeague(
            prizeSettings: new List<LeaguePrizeSetting> { firstPrize, secondPrize, thirdPrize },
            members: new List<(string UserId, int Points)>
            {
                ("user-1", 100),
                ("user-2", 80),
                ("user-3", 80)
            });
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(league);

        IEnumerable<Winning>? capturedWinnings = null;
        await _winningsRepository.AddWinningsAsync(
            Arg.Do<IEnumerable<Winning>>(w => capturedWinnings = w.ToList()),
            Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        var winnings = capturedWinnings!.ToList();
        winnings.Should().HaveCount(3);
        winnings.Single(w => w.UserId == "user-1").Amount.Should().Be(220m);
        winnings.Single(w => w.UserId == "user-2").Amount.Should().Be(90m);
        winnings.Single(w => w.UserId == "user-2").LeaguePrizeSettingId.Should().Be(12);
        winnings.Single(w => w.UserId == "user-3").Amount.Should().Be(90m);
        winnings.Single(w => w.UserId == "user-3").LeaguePrizeSettingId.Should().Be(12);
        winnings.Sum(w => w.Amount).Should().Be(400m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldPoolAllRanksIntoJointFirst_WhenAllMembersAreJointFirst()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        var firstPrize = CreatePrizeSetting(11, PrizeType.Overall, 1, 200m);
        var secondPrize = CreatePrizeSetting(12, PrizeType.Overall, 2, 120m);
        var thirdPrize = CreatePrizeSetting(13, PrizeType.Overall, 3, 60m);
        var league = CreateLeague(
            prizeSettings: new List<LeaguePrizeSetting> { firstPrize, secondPrize, thirdPrize },
            members: new List<(string UserId, int Points)>
            {
                ("user-1", 100),
                ("user-2", 100),
                ("user-3", 100),
                ("user-4", 80)
            });
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(league);

        IEnumerable<Winning>? capturedWinnings = null;
        await _winningsRepository.AddWinningsAsync(
            Arg.Do<IEnumerable<Winning>>(w => capturedWinnings = w.ToList()),
            Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        var winnings = capturedWinnings!.ToList();
        winnings.Should().HaveCount(3);
        winnings.Sum(w => w.Amount).Should().Be(380m);
        winnings.Where(w => w.UserId.StartsWith("user-1") || w.UserId == "user-2" || w.UserId == "user-3")
            .Should().AllSatisfy(w => w.LeaguePrizeSettingId.Should().Be(11));
        winnings.Where(w => w.UserId is "user-1" or "user-2" or "user-3")
            .Sum(w => w.Amount).Should().Be(380m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldSkipRankingGroup_WhenNoPrizeSettingCoversThatRange()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        var firstPrize = CreatePrizeSetting(11, PrizeType.Overall, 1, 100m);
        var league = CreateLeague(
            prizeSettings: new List<LeaguePrizeSetting> { firstPrize },
            members: new List<(string UserId, int Points)>
            {
                ("user-1", 100),
                ("user-2", 80),
                ("user-3", 60)
            });
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(league);

        IEnumerable<Winning>? capturedWinnings = null;
        await _winningsRepository.AddWinningsAsync(
            Arg.Do<IEnumerable<Winning>>(w => capturedWinnings = w.ToList()),
            Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        var winnings = capturedWinnings!.ToList();
        winnings.Should().HaveCount(1);
        winnings.Single().UserId.Should().Be("user-1");
        winnings.Single().Amount.Should().Be(100m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldSkipRankingGroup_WhenPooledAmountIsZero()
    {
        SetupRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(true);
        var firstPrize = CreatePrizeSetting(11, PrizeType.Overall, 1, 0m);
        var secondPrize = CreatePrizeSetting(12, PrizeType.Overall, 2, 50m);
        var league = CreateLeague(
            prizeSettings: new List<LeaguePrizeSetting> { firstPrize, secondPrize },
            members: new List<(string UserId, int Points)>
            {
                ("user-1", 100),
                ("user-2", 80)
            });
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(league);

        IEnumerable<Winning>? capturedWinnings = null;
        await _winningsRepository.AddWinningsAsync(
            Arg.Do<IEnumerable<Winning>>(w => capturedWinnings = w.ToList()),
            Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CreateCommand(), CancellationToken.None);

        var winnings = capturedWinnings!.ToList();
        winnings.Should().HaveCount(1);
        winnings.Single().UserId.Should().Be("user-2");
        winnings.Single().Amount.Should().Be(50m);
    }

    private static ProcessPrizesCommand CreateCommand() =>
        new() { LeagueId = LeagueId, RoundId = RoundId };

    private void SetupRound()
    {
        var round = new Round(
            id: RoundId,
            seasonId: SeasonId,
            roundNumber: 38,
            displayName: "Round 38",
            startDateUtc: FixedNow.AddDays(-7),
            deadlineUtc: FixedNow.AddDays(-7),
            status: RoundStatus.Completed,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: null);
        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns(round);
    }

    private static LeaguePrizeSetting CreatePrizeSetting(int id, PrizeType prizeType, int rank, decimal amount)
    {
        var setting = LeaguePrizeSetting.Create(LeagueId, prizeType, rank, amount);
        typeof(LeaguePrizeSetting).GetProperty(nameof(LeaguePrizeSetting.Id))!
            .SetValue(setting, id);
        return setting;
    }

    private static League CreateLeague(
        List<LeaguePrizeSetting> prizeSettings,
        List<(string UserId, int Points)> members)
    {
        var leagueMembers = members.Select(m => new LeagueMember(
            leagueId: LeagueId,
            userId: m.UserId,
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false,
            isArchivedByUser: false,
            joinedAtUtc: FixedNow.AddDays(-60),
            approvedAtUtc: FixedNow.AddDays(-60),
            roundResults: new List<LeagueRoundResult>
            {
                new(
                    leagueId: LeagueId,
                    roundId: RoundId,
                    userId: m.UserId,
                    basePoints: m.Points,
                    boostedPoints: m.Points,
                    hasBoost: false,
                    appliedBoostCode: null,
                    exactScoreCount: 0)
            })).ToList();

        return new League(
            id: LeagueId,
            name: "Test League",
            seasonId: SeasonId,
            administratorUserId: "admin",
            entryCode: "ABC123",
            createdAtUtc: FixedNow.AddDays(-90),
            entryDeadlineUtc: FixedNow.AddDays(-60),
            pointsForExactScore: 3,
            pointsForCorrectResult: 1,
            price: 100m,
            isFree: false,
            hasPrizes: true,
            prizeFundOverride: null,
            members: leagueMembers,
            prizeSettings: prizeSettings);
    }
}
