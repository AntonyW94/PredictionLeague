using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Features.Badges.Commands;
using ThePredictions.Application.Features.Badges.Evaluation;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges.Commands;

/// <summary>
/// Awards every badge earned in a round. Two things matter beyond the thresholds: it must be safe to
/// re-run (re-completing a round must not re-award), and each badge has to be dated to when it was
/// actually earned rather than to whenever the job happened to run.
/// </summary>
public class EvaluateBadgesForRoundCommandHandlerTests
{
    private const int RoundId = 100;
    private const int SeasonId = 7;
    private const int RoundNumber = 5;
    private const int LeagueId = 3;

    private static readonly DateTime Now = new(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastKickOff = new(2026, 5, 20, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CompletedUtc = new(2026, 5, 21, 9, 0, 0, DateTimeKind.Utc);

    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IBadgeEvaluationRepository _evaluation = Substitute.For<IBadgeEvaluationRepository>();
    private readonly IUserBadgeRepository _userBadges = Substitute.For<IUserBadgeRepository>();

    private readonly List<AwardedBadge> _awarded = [];

    public EvaluateBadgesForRoundCommandHandlerTests()
    {
        // Every award is new unless a test says otherwise.
        _userBadges.AwardAsync(Arg.Do<AwardedBadge>(_awarded.Add), Arg.Any<CancellationToken>()).Returns(true);

        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetSeasonCumulativeExactsAsync(SeasonId, RoundNumber, Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetStreaksEndingAtRoundAsync(SeasonId, RoundNumber, Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetRoundWinnersAsync(RoundId, Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetBeatTheCrowdUsersAsync(RoundId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetSocialiteAwardsAsync(Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetAccountBadgeAwardsAsync(Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetMonthWinnersAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetStageWinnersAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetSeasonStandingsAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
        _evaluation.GetEverPresentUsersAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
    }

    private EvaluateBadgesForRoundCommandHandler BuildHandler() =>
        new(_roundRepository, _evaluation, _userBadges, new TestDateTimeProvider(Now));

    private Round GivenRound(DateTime? completedUtc = null, bool withMatches = true)
    {
        var matches = withMatches
            ? new List<Match>
            {
                new(id: 1, roundId: RoundId, homeTeamId: 1, awayTeamId: 2, matchDateTimeUtc: LastKickOff.AddDays(-1),
                    customLockTimeUtc: null, status: MatchStatus.Completed, actualHomeTeamScore: 1, actualAwayTeamScore: 0,
                    externalId: null, matchNumber: 1, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null),
                new(id: 2, roundId: RoundId, homeTeamId: 3, awayTeamId: 4, matchDateTimeUtc: LastKickOff,
                    customLockTimeUtc: null, status: MatchStatus.Completed, actualHomeTeamScore: 2, actualAwayTeamScore: 2,
                    externalId: null, matchNumber: 2, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null)
            }
            : null;

        // CompletedDateUtc is only stamped on the transition into Completed, so a round that needs
        // one starts InProgress and is moved across.
        var round = new Round(
            id: RoundId, seasonId: SeasonId, roundNumber: RoundNumber, displayName: "Round 5",
            startDateUtc: LastKickOff.AddDays(-2), deadlineUtc: LastKickOff.AddDays(-1),
            status: completedUtc.HasValue ? RoundStatus.InProgress : RoundStatus.Completed,
            apiRoundName: null, lastReminderSentUtc: null, matches: matches);

        if (completedUtc.HasValue)
            round.UpdateStatus(RoundStatus.Completed, new TestDateTimeProvider(completedUtc.Value));

        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns(round);
        return round;
    }

    private Task<IReadOnlyList<RoundBadgeAward>> HandleAsync() =>
        BuildHandler().Handle(new EvaluateBadgesForRoundCommand(RoundId), CancellationToken.None);

    private IEnumerable<string> KeysFor(string userId) =>
        _awarded.Where(a => a.UserId == userId).Select(a => a.BadgeKey);

    // ---------- guards ----------

    [Fact]
    public async Task Handle_ShouldAwardNothing_WhenTheRoundDoesNotExist()
    {
        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns((Round?)null);

        var result = await HandleAsync();

        result.Should().BeEmpty();
        await _userBadges.DidNotReceiveWithAnyArgs().AwardAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReportOnlyGenuinelyNewAwards()
    {
        // Re-completing a round runs this again; the digest must not celebrate badges the player
        // already had.
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 0, 0)]);
        _userBadges.AwardAsync(Arg.Any<AwardedBadge>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await HandleAsync();

        result.Should().BeEmpty();
    }

    // ---------- dating the award ----------

    [Fact]
    public async Task Handle_ShouldDateAwardsToWhenTheRoundCompleted()
    {
        GivenRound(completedUtc: CompletedUtc);
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 0, 0)]);

        await HandleAsync();

        _awarded.Should().OnlyContain(a => a.AwardedUtc == CompletedUtc);
    }

    [Fact]
    public async Task Handle_ShouldFallBackToTheLastKickOff_WhenCompletionWasNeverRecorded()
    {
        // Retrospective awards belong to when the round actually finished, not to today.
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 0, 0)]);

        await HandleAsync();

        _awarded.Should().OnlyContain(a => a.AwardedUtc == LastKickOff);
    }

    [Fact]
    public async Task Handle_ShouldFallBackToTheClock_ForARoundWithNoMatches()
    {
        GivenRound(withMatches: false);
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 0, 0)]);

        await HandleAsync();

        _awarded.Should().OnlyContain(a => a.AwardedUtc == Now);
    }

    // ---------- first steps ----------

    [Fact]
    public async Task Handle_ShouldAwardOffTheMark_ToAnyoneWhoPredicted()
    {
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 0, 0)]);

        await HandleAsync();

        KeysFor("user-1").Should().Contain(BadgeKeys.OffTheMark);
    }

    [Fact]
    public async Task Handle_ShouldNotAwardOnTheBoard_ToSomeoneWhoScoredNothing()
    {
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 0, 0)]);

        await HandleAsync();

        KeysFor("user-1").Should().NotContain(BadgeKeys.OnTheBoard);
    }

    [Fact]
    public async Task Handle_ShouldAwardOnTheBoard_ForACorrectResultAlone()
    {
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 0, 2)]);

        await HandleAsync();

        KeysFor("user-1").Should().Contain(BadgeKeys.OnTheBoard).And.NotContain(BadgeKeys.FirstBlood);
    }

    [Fact]
    public async Task Handle_ShouldAwardFirstBlood_OnTheFirstExactScore()
    {
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 1, 1)]);

        await HandleAsync();

        KeysFor("user-1").Should().Contain(BadgeKeys.FirstBlood).And.Contain(BadgeKeys.OnTheBoard);
    }

    // ---------- Sharpshooter ----------

    [Theory]
    [InlineData(2, new string[0])]
    [InlineData(3, new[] { BadgeKeys.Sharpshooter1 })]
    [InlineData(4, new[] { BadgeKeys.Sharpshooter1, BadgeKeys.Sharpshooter2 })]
    [InlineData(5, new[] { BadgeKeys.Sharpshooter1, BadgeKeys.Sharpshooter2, BadgeKeys.Sharpshooter3 })]
    public async Task Handle_ShouldAwardEverySharpshooterTierReached(int exactScores, string[] expected)
    {
        // Reaching tier 3 also earns tiers 1 and 2, so a first-time player leaps straight to gold.
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", exactScores, 0)]);

        await HandleAsync();

        foreach (var key in expected)
            KeysFor("user-1").Should().Contain(key);

        KeysFor("user-1").Count(k => k.StartsWith("sharpshooter")).Should().Be(expected.Length);
    }

    [Fact]
    public async Task Handle_ShouldScopeSharpshooterToTheRoundAndRecordTheCount()
    {
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 3, 0)]);

        await HandleAsync();

        var award = _awarded.Single(a => a.BadgeKey == BadgeKeys.Sharpshooter1);
        award.RoundId.Should().Be(RoundId);
        award.Detail.Should().Be("3 in a round");
    }

    // ---------- Marksman and On Fire ----------

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(15, 3)]
    public async Task Handle_ShouldAwardEveryMarksmanTierReached(int seasonExacts, int expectedCount)
    {
        GivenRound();
        _evaluation.GetSeasonCumulativeExactsAsync(SeasonId, RoundNumber, Arg.Any<CancellationToken>())
            .Returns([new UserCount("user-1", seasonExacts)]);

        await HandleAsync();

        KeysFor("user-1").Count(k => k.StartsWith("marksman")).Should().Be(expectedCount);
    }

    [Fact]
    public async Task Handle_ShouldScopeMarksmanToTheSeason()
    {
        GivenRound();
        _evaluation.GetSeasonCumulativeExactsAsync(SeasonId, RoundNumber, Arg.Any<CancellationToken>())
            .Returns([new UserCount("user-1", 5)]);

        await HandleAsync();

        var award = _awarded.Single(a => a.BadgeKey == BadgeKeys.Marksman1);
        award.SeasonId.Should().Be(SeasonId);
        award.Detail.Should().Be("5 exact scores");
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 2)]
    [InlineData(7, 3)]
    public async Task Handle_ShouldAwardEveryOnFireTierReached(int streak, int expectedCount)
    {
        GivenRound();
        _evaluation.GetStreaksEndingAtRoundAsync(SeasonId, RoundNumber, Arg.Any<CancellationToken>())
            .Returns([new UserCount("user-1", streak)]);

        await HandleAsync();

        KeysFor("user-1").Count(k => k.StartsWith("on-fire")).Should().Be(expectedCount);
    }

    // ---------- round winners and the crowd ----------

    [Fact]
    public async Task Handle_ShouldAwardRoundWinnerPerLeague()
    {
        // The same player can top two leagues in the same round and earns it in each.
        GivenRound();
        _evaluation.GetRoundWinnersAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new UserLeague("user-1", 3), new UserLeague("user-1", 9)]);

        await HandleAsync();

        _awarded.Where(a => a.BadgeKey == BadgeKeys.RoundWinner)
            .Select(a => a.LeagueId).Should().BeEquivalentTo([3, 9]);
    }

    [Fact]
    public async Task Handle_ShouldAwardBeatTheCrowd_OnlyOnceACrowdIsBigEnough()
    {
        GivenRound();
        _evaluation.GetBeatTheCrowdUsersAsync(RoundId, 5, Arg.Any<CancellationToken>()).Returns(["user-1"]);

        await HandleAsync();

        KeysFor("user-1").Should().Contain(BadgeKeys.BeatTheCrowd);
        await _evaluation.Received(1).GetBeatTheCrowdUsersAsync(RoundId, 5, Arg.Any<CancellationToken>());
    }

    // ---------- Socialite ----------

    [Theory]
    [InlineData(1, BadgeKeys.Socialite1)]
    [InlineData(3, BadgeKeys.Socialite2)]
    [InlineData(5, BadgeKeys.Socialite3)]
    public async Task Handle_ShouldAwardTheSocialiteTierForThatMilestone(int rank, string expectedKey)
    {
        GivenRound();
        var joinedUtc = new DateTime(2025, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        _evaluation.GetSocialiteAwardsAsync(Arg.Any<CancellationToken>())
            .Returns([new SocialiteAward("user-1", rank, joinedUtc)]);

        await HandleAsync();

        var award = _awarded.Single(a => a.BadgeKey == expectedKey);
        award.AwardedUtc.Should().Be(joinedUtc, "the badge belongs to when they joined that league");
        award.Detail.Should().Be($"{rank} leagues");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task Handle_ShouldIgnoreALeagueCountThatIsNotAMilestone(int rank)
    {
        GivenRound();
        _evaluation.GetSocialiteAwardsAsync(Arg.Any<CancellationToken>())
            .Returns([new SocialiteAward("user-1", rank, Now)]);

        await HandleAsync();

        _awarded.Should().BeEmpty();
    }

    // ---------- account, month and stage ----------

    [Fact]
    public async Task Handle_ShouldAwardAccountBadgesDatedToWhenTheyWereEarned()
    {
        GivenRound();
        var addedUtc = new DateTime(2025, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        _evaluation.GetAccountBadgeAwardsAsync(Arg.Any<CancellationToken>())
            .Returns([new AccountBadgeAward("user-1", BadgeKeys.OnCall, addedUtc)]);

        await HandleAsync();

        _awarded.Single().AwardedUtc.Should().Be(addedUtc);
    }

    [Fact]
    public async Task Handle_ShouldAwardMonthAndStageWinnersWithTheirOwnDates()
    {
        GivenRound();
        var monthEnd = new DateTime(2026, 4, 30, 20, 0, 0, DateTimeKind.Utc);
        _evaluation.GetMonthWinnersAsync(SeasonId, Arg.Any<CancellationToken>())
            .Returns([new MonthStageWinner("user-1", LeagueId, 88, monthEnd, "April")]);
        _evaluation.GetStageWinnersAsync(SeasonId, Arg.Any<CancellationToken>())
            .Returns([new MonthStageWinner("user-2", LeagueId, 99, monthEnd, "Group stage")]);

        await HandleAsync();

        var month = _awarded.Single(a => a.BadgeKey == BadgeKeys.MonthWinner);
        month.RoundId.Should().Be(88);
        month.Detail.Should().Be("April");
        month.AwardedUtc.Should().Be(monthEnd);

        var stage = _awarded.Single(a => a.BadgeKey == BadgeKeys.StageWinner);
        stage.RoundId.Should().Be(99);
        stage.Detail.Should().Be("Group stage");
    }

    // ---------- season-end honours ----------

    [Fact]
    public async Task Handle_ShouldNotAwardSeasonHonours_BeforeTheFinalRound()
    {
        GivenRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);

        await HandleAsync();

        await _evaluation.DidNotReceiveWithAnyArgs().GetSeasonStandingsAsync(default, CancellationToken.None);
        await _evaluation.DidNotReceiveWithAnyArgs().GetEverPresentUsersAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldAwardChampionAndPodium_OnTheFinalRound()
    {
        GivenRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>()).Returns(true);
        _evaluation.GetSeasonStandingsAsync(SeasonId, Arg.Any<CancellationToken>())
            .Returns([
                new UserLeagueRank("winner", LeagueId, 1),
                new UserLeagueRank("third", LeagueId, 3),
                new UserLeagueRank("fourth", LeagueId, 4)
            ]);

        await HandleAsync();

        KeysFor("winner").Should().BeEquivalentTo([BadgeKeys.Champion, BadgeKeys.Podium]);
        KeysFor("third").Should().BeEquivalentTo([BadgeKeys.Podium]);
        KeysFor("fourth").Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAwardEverPresent_ScopedToTheSeason()
    {
        GivenRound();
        _roundRepository.IsLastRoundOfSeasonAsync(RoundId, SeasonId, Arg.Any<CancellationToken>()).Returns(true);
        _evaluation.GetEverPresentUsersAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(["user-1"]);

        await HandleAsync();

        var award = _awarded.Single(a => a.BadgeKey == BadgeKeys.EverPresent);
        award.SeasonId.Should().Be(SeasonId);
    }

    [Fact]
    public async Task Handle_ShouldReturnEveryNewAwardForTheDigest()
    {
        GivenRound();
        _evaluation.GetRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new RoundUserResult("user-1", 3, 2)]);

        var result = await HandleAsync();

        result.Should().OnlyContain(a => a.UserId == "user-1");
        result.Select(a => a.BadgeKey).Should().Contain(BadgeKeys.Sharpshooter1);
    }
}
