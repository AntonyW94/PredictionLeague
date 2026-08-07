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

public class SectionPrizeStrategyTests
{
    private readonly IWinningsRepository _winningsRepository = Substitute.For<IWinningsRepository>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly ITournamentRoundMappingRepository _mappingRepository = Substitute.For<ITournamentRoundMappingRepository>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly SectionPrizeStrategy _strategy;

    private static readonly DateTime FixedNow = new(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc);

    private const int LeagueId = 1;
    private const int SeasonId = 1;
    private const string GroupStage = "Group stage";
    private const string KnockoutStage = "Knockout stage";

    public SectionPrizeStrategyTests()
    {
        _strategy = new SectionPrizeStrategy(
            _winningsRepository,
            _roundRepository,
            _mappingRepository,
            _leagueRepository,
            new TestDateTimeProvider(FixedNow));
    }

    /// <summary>
    /// Standard tournament shape: rounds 1-2 are group stage, rounds 3-4 are knockouts.
    /// Round id = 100 + round number.
    /// </summary>
    private void SetupSeason(params RoundStatus[] roundStatuses)
    {
        var rounds = new Dictionary<int, Round>();
        for (var number = 1; number <= roundStatuses.Length; number++)
        {
            var id = 100 + number;
            rounds[id] = new Round(
                id: id, seasonId: SeasonId, roundNumber: number, displayName: $"Round {number}",
                startDateUtc: FixedNow.AddDays(-30 + number), deadlineUtc: FixedNow.AddDays(-30 + number),
                status: roundStatuses[number - 1], apiRoundName: null, lastReminderSentUtc: null, matches: null);
        }

        _roundRepository.GetAllForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(rounds);
        _roundRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(args => rounds.GetValueOrDefault((int)args[0]));

        _mappingRepository.GetBySeasonIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(
        [
            new TournamentRoundMapping(1, SeasonId, 1, "Matchday 1", "Group1", 8),
            new TournamentRoundMapping(2, SeasonId, 2, "Matchday 2", "Group2|Group3", 8),
            new TournamentRoundMapping(3, SeasonId, 3, "Semi-finals", "SemiFinals", 2),
            new TournamentRoundMapping(4, SeasonId, 4, "Final", "Final", 1)
        ]);
    }

    private static LeaguePrizeSetting CreateStageSetting(int id, string stage, int rank, decimal amount)
    {
        var setting = LeaguePrizeSetting.Create(LeagueId, PrizeType.Stages, rank, amount, stage);
        typeof(LeaguePrizeSetting).GetProperty(nameof(LeaguePrizeSetting.Id))!.SetValue(setting, id);
        return setting;
    }

    private void SetupLeague(List<LeaguePrizeSetting> prizeSettings, params (string UserId, int RoundNumber, int Points)[] results)
    {
        var members = results
            .GroupBy(r => r.UserId)
            .Select(g => new LeagueMember(
                leagueId: LeagueId, userId: g.Key, status: LeagueMemberStatus.Approved,
                isAlertDismissed: false, isArchivedByUser: false,
                joinedAtUtc: FixedNow.AddDays(-60), approvedAtUtc: FixedNow.AddDays(-60),
                roundResults: g.Select(r => new LeagueRoundResult(
                    leagueId: LeagueId, roundId: 100 + r.RoundNumber, userId: g.Key,
                    basePoints: r.Points, boostedPoints: r.Points, hasBoost: false,
                    appliedBoostCode: null, exactScoreCount: 0)).ToList()))
            .ToList();

        var league = new League(
            id: LeagueId, name: "Test League", seasonId: SeasonId, administratorUserId: "admin",
            entryCode: "ABC123", createdAtUtc: FixedNow.AddDays(-90), entryDeadlineUtc: FixedNow.AddDays(-60),
            pointsForExactScore: 3, pointsForCorrectResult: 1, price: 25m, isFree: false, hasPrizes: true,
            prizeFundOverride: null, members: members, prizeSettings: prizeSettings);

        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(league);
    }

    private static ProcessPrizesCommand CommandForRound(int roundNumber) =>
        new() { LeagueId = LeagueId, RoundId = 100 + roundNumber };

    [Fact]
    public void PrizeType_ShouldBeStages() => _strategy.PrizeType.Should().Be(PrizeType.Stages);

    [Fact]
    public async Task AwardPrizes_ShouldAwardGroupStage_WhenLastGroupRoundCompletesMidSeason()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        SetupLeague(
            [CreateStageSetting(10, GroupStage, 1, 45m), CreateStageSetting(11, KnockoutStage, 1, 45m)],
            ("user-1", 1, 10), ("user-1", 2, 10),
            ("user-2", 1, 5), ("user-2", 2, 5));

        IEnumerable<Winning>? captured = null;
        await _winningsRepository.AddWinningsAsync(Arg.Do<IEnumerable<Winning>>(w => captured = w.ToList()), Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        await _winningsRepository.Received(1).DeleteWinningsForStageAsync(LeagueId, GroupStage, Arg.Any<CancellationToken>());
        await _winningsRepository.DidNotReceive().DeleteWinningsForStageAsync(LeagueId, KnockoutStage, Arg.Any<CancellationToken>());

        var winnings = captured!.ToList();
        winnings.Should().ContainSingle();
        winnings[0].UserId.Should().Be("user-1");
        winnings[0].Amount.Should().Be(45m);
        winnings[0].LeaguePrizeSettingId.Should().Be(10);
    }

    [Fact]
    public async Task AwardPrizes_ShouldNotAwardAnything_WhenNoStageIsCompleteYet()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.InProgress, RoundStatus.Published, RoundStatus.Published);
        SetupLeague(
            [CreateStageSetting(10, GroupStage, 1, 45m)],
            ("user-1", 1, 10));

        await _strategy.AwardPrizes(CommandForRound(1), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().DeleteWinningsForStageAsync(default, default!, CancellationToken.None);
        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldAwardBothStages_WhenFinalRoundCompletes()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Completed);
        SetupLeague(
            [CreateStageSetting(10, GroupStage, 1, 45m), CreateStageSetting(11, KnockoutStage, 1, 45m)],
            ("user-1", 1, 10), ("user-1", 3, 1),
            ("user-2", 1, 5), ("user-2", 3, 20));

        var captured = new List<Winning>();
        await _winningsRepository.AddWinningsAsync(Arg.Do<IEnumerable<Winning>>(w => captured.AddRange(w)), Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CommandForRound(4), CancellationToken.None);

        await _winningsRepository.Received(1).DeleteWinningsForStageAsync(LeagueId, GroupStage, Arg.Any<CancellationToken>());
        await _winningsRepository.Received(1).DeleteWinningsForStageAsync(LeagueId, KnockoutStage, Arg.Any<CancellationToken>());

        captured.Should().HaveCount(2);
        captured.Single(w => w.LeaguePrizeSettingId == 10).UserId.Should().Be("user-1");
        captured.Single(w => w.LeaguePrizeSettingId == 11).UserId.Should().Be("user-2");
    }

    [Fact]
    public async Task AwardPrizes_ShouldPoolPrizesAcrossTiedMembers_WhenStageScoresAreEqual()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        SetupLeague(
            [CreateStageSetting(10, GroupStage, 1, 30m), CreateStageSetting(11, GroupStage, 2, 10m)],
            ("user-1", 1, 10),
            ("user-2", 1, 10));

        IEnumerable<Winning>? captured = null;
        await _winningsRepository.AddWinningsAsync(Arg.Do<IEnumerable<Winning>>(w => captured = w.ToList()), Arg.Any<CancellationToken>());

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        var winnings = captured!.ToList();
        winnings.Should().HaveCount(2);
        winnings.Sum(w => w.Amount).Should().Be(40m);
        winnings.Select(w => w.Amount).Should().OnlyContain(a => a == 20m);
    }

    [Fact]
    public async Task AwardPrizes_ShouldReturnEarly_WhenLeagueHasNoStagePrizeSettings()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        SetupLeague([], ("user-1", 1, 10));

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().DeleteWinningsForStageAsync(default, default!, CancellationToken.None);
        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldReturnEarly_WhenRoundNotFound()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);

        await _strategy.AwardPrizes(new ProcessPrizesCommand { LeagueId = LeagueId, RoundId = 999 }, CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheRoundIsNotInTheSeason()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        SetupLeague([CreateStageSetting(10, GroupStage, 1, 45m)], ("user-1", 1, 10));

        await _strategy.AwardPrizes(new ProcessPrizesCommand { LeagueId = LeagueId, RoundId = 999 }, CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenTheLeagueIsGone()
    {
        // The round finished but the league was deleted in between, so there is nobody to pay.
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        _leagueRepository.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((League?)null);

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldDoNothing_WhenNoStagePrizesAreConfigured()
    {
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        SetupLeague([], ("user-1", 1, 10));

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldSkipAStageWithNothingToPayOut()
    {
        // A stage configured at zero pounds is effectively off; nobody should get a nil winning.
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        SetupLeague(
            [CreateStageSetting(10, GroupStage, 1, 0m)],
            ("user-1", 1, 10), ("user-1", 2, 10));

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldPayNobody_WhenThePrizeIsForARankNobodyReached()
    {
        // Only one entrant, but the stage pays second place only - there is no second place.
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed, RoundStatus.Published, RoundStatus.Published);
        SetupLeague(
            [CreateStageSetting(10, GroupStage, 2, 45m)],
            ("user-1", 1, 10), ("user-1", 2, 10));

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        await _winningsRepository.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task AwardPrizes_ShouldIgnoreARoundNumberWithNoMatchingRound()
    {
        // A mapping can name a round number the season never created; it must be skipped rather
        // than dragging a null into the stage.
        SetupSeason(RoundStatus.Completed, RoundStatus.Completed);
        SetupLeague(
            [CreateStageSetting(10, GroupStage, 1, 45m)],
            ("user-1", 1, 10), ("user-1", 2, 10));

        await _strategy.AwardPrizes(CommandForRound(2), CancellationToken.None);

        await _winningsRepository.Received(1).AddWinningsAsync(Arg.Any<IEnumerable<Winning>>(), Arg.Any<CancellationToken>());
    }
}
