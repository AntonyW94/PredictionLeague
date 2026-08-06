using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Strategies;

/// <summary>
/// Shared scaffolding for the prize strategies. They all read a round and a league, decide whether
/// this is the moment to pay out, and then write winnings - so the arrange step is the same shape
/// each time and only the trigger condition differs.
/// </summary>
internal sealed class PrizeStrategyScenario
{
    public const int LeagueId = 1;
    public const int RoundId = 100;
    public const int SeasonId = 1;
    public const int RoundNumber = 38;

    public static readonly DateTime FixedNow = new(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);

    public IWinningsRepository Winnings { get; } = Substitute.For<IWinningsRepository>();
    public IRoundRepository Rounds { get; } = Substitute.For<IRoundRepository>();
    public ILeagueRepository Leagues { get; } = Substitute.For<ILeagueRepository>();

    public static ProcessPrizesCommand Command => new() { LeagueId = LeagueId, RoundId = RoundId };

    public Round GivenRound(DateTime? startDateUtc = null)
    {
        var round = new Round(
            id: RoundId,
            seasonId: SeasonId,
            roundNumber: RoundNumber,
            displayName: $"Round {RoundNumber}",
            startDateUtc: startDateUtc ?? FixedNow.AddDays(-7),
            deadlineUtc: (startDateUtc ?? FixedNow.AddDays(-7)).AddHours(1),
            status: RoundStatus.Completed,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: null);

        Rounds.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns(round);
        return round;
    }

    public void GivenNoRound() => Rounds.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns((Round?)null);

    public void GivenNoLeague() => Leagues.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((League?)null);

    public League GivenLeague(
        IEnumerable<LeaguePrizeSetting> prizeSettings,
        params (string UserId, int Points, int ExactScores)[] members)
    {
        var league = BuildLeague(prizeSettings.ToList(), members);
        Leagues.GetByIdWithAllDataAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(league);
        return league;
    }

    public List<Winning> CapturedWinnings { get; } = [];

    public void CaptureWinnings() =>
        Winnings.AddWinningsAsync(
            Arg.Do<IEnumerable<Winning>>(w => CapturedWinnings.AddRange(w)),
            Arg.Any<CancellationToken>());

    public static LeaguePrizeSetting PrizeSetting(int id, PrizeType prizeType, decimal amount, int rank = 1)
    {
        var setting = LeaguePrizeSetting.Create(LeagueId, prizeType, rank, amount);

        // Id is set by Dapper in production; there is no other way to give it one here.
        typeof(LeaguePrizeSetting).GetProperty(nameof(LeaguePrizeSetting.Id))!.SetValue(setting, id);

        return setting;
    }

    private static League BuildLeague(
        List<LeaguePrizeSetting> prizeSettings,
        (string UserId, int Points, int ExactScores)[] members)
    {
        var leagueMembers = members.Select(m => new LeagueMember(
            leagueId: LeagueId,
            userId: m.UserId,
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false,
            isArchivedByUser: false,
            joinedAtUtc: FixedNow.AddDays(-60),
            approvedAtUtc: FixedNow.AddDays(-60),
            roundResults:
            [
                new LeagueRoundResult(
                    leagueId: LeagueId,
                    roundId: RoundId,
                    userId: m.UserId,
                    basePoints: m.Points,
                    boostedPoints: m.Points,
                    hasBoost: false,
                    appliedBoostCode: null,
                    exactScoreCount: m.ExactScores)
            ])).ToList();

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
