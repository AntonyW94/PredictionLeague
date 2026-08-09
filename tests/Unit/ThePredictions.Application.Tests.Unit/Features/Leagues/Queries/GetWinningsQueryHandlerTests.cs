using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;
using static ThePredictions.Application.Features.Leagues.Queries.GetWinningsQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The league's prize table. Reading it back is not a mapping: the handler pads every unwon round and
/// unwon month with a placeholder at the configured amount, so a player sees the whole season rather
/// than only the parts already decided, and it aggregates each member's winnings by prize type for the
/// leaderboard. Both of those are shaping rules that live only here, and the month padding has to cross
/// a calendar year because a season runs August to May.
/// </summary>
public class GetWinningsQueryHandlerTests
{
    private const int LeagueId = 10;
    private const string CurrentUserId = "user-x";

    private static readonly DateTime NowUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PassedDeadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FutureDeadline = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly GetWinningsQueryHandler _handler;

    public GetWinningsQueryHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _handler = new GetWinningsQueryHandler(_dbConnection, _membershipService, _dateTimeProvider);
    }

    // ---------- arrange helpers ----------

    private void GivenLeague(
        DateTime? entryDeadlineUtc = null,
        int entryCount = 4,
        decimal entryCost = 10m,
        int totalRoundsInSeason = 3,
        DateTime? seasonStartUtc = null,
        DateTime? seasonEndUtc = null)
    {
        _dbConnection.QuerySingleOrDefaultAsync<LeagueData>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new LeagueData
            {
                EntryDeadlineUtc = entryDeadlineUtc ?? PassedDeadline,
                EntryCost = entryCost,
                EntryCount = entryCount,
                TotalRoundsInSeason = totalRoundsInSeason,
                SeasonStartDateUtc = seasonStartUtc ?? new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                SeasonEndDateUtc = seasonEndUtc ?? new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            });
    }

    private void GivenNoLeague() =>
        _dbConnection.QuerySingleOrDefaultAsync<LeagueData>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns((LeagueData?)null);

    private void GivenPrizeSettings(params PrizeSettingQueryResult[] settings) =>
        _dbConnection.QueryAsync<PrizeSettingQueryResult>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(settings);

    private void GivenWinnings(params WinningsQueryResult[] winnings) =>
        _dbConnection.QueryAsync<WinningsQueryResult>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(winnings);

    private void GivenMembers(params LeagueMemberQueryResult[] members) =>
        _dbConnection.QueryAsync<LeagueMemberQueryResult>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(members);

    private static PrizeSettingQueryResult Setting(int id, PrizeType type, string name, decimal amount, string? stage = null) =>
        new(id, type, name, amount, stage);

    private static WinningsQueryResult Winner(
        int settingId, PrizeType type, string name, decimal amount, string userId, int? roundNumber = null, int? month = null) =>
        new(amount, settingId, type, name, roundNumber, month, userId);

    private Task<WinningsDto> HandleAsync() =>
        _handler.Handle(new GetWinningsQuery(LeagueId, CurrentUserId), CancellationToken.None);

    // ---------- membership and missing league ----------

    [Fact]
    public async Task Handle_ShouldEnforceMembership_BeforeReadingAnything()
    {
        _membershipService.EnsureApprovedMemberAsync(LeagueId, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        var act = HandleAsync;

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyResult_WhenTheLeagueDoesNotExist()
    {
        GivenNoLeague();

        var result = await HandleAsync();

        result.WinningsCalculated.Should().BeFalse();
        result.RoundPrizes.Should().BeEmpty();
        result.Leaderboard.Entries.Should().BeEmpty();
    }

    // ---------- the "not yet calculated" gate ----------

    // Before the entry deadline the entrant count can still change, so the prize pot is provisional and
    // no prize is attributed to anyone yet.
    [Fact]
    public async Task Handle_ShouldReportWinningsAsNotCalculated_WhenTheEntryDeadlineHasNotPassed()
    {
        GivenLeague(entryDeadlineUtc: FutureDeadline, entryCount: 5, entryCost: 20m);
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));

        var result = await HandleAsync();

        result.WinningsCalculated.Should().BeFalse();
        result.EntryCount.Should().Be(5);
        result.EntryCost.Should().Be(20m);
        result.TotalPrizePot.Should().Be(100m);
        result.RoundPrizes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReportWinningsAsNotCalculated_WhenTheLeagueHasNoPrizeSettings()
    {
        GivenLeague(entryCount: 3, entryCost: 10m);
        GivenPrizeSettings();

        var result = await HandleAsync();

        result.WinningsCalculated.Should().BeFalse();
        result.TotalPrizePot.Should().Be(30m);
    }

    [Fact]
    public async Task Handle_ShouldReportWinningsAsCalculated_OnceTheDeadlineHasPassedAndPrizesExist()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));

        var result = await HandleAsync();

        result.WinningsCalculated.Should().BeTrue();
    }

    // ---------- round prizes ----------

    // The padding rule: every round in the season appears, whether or not it has been won, so the table
    // shows the full season rather than only the rounds already decided.
    [Fact]
    public async Task Handle_ShouldPadEveryUnwonRound_WithTheConfiguredAmount()
    {
        GivenLeague(totalRoundsInSeason: 3);
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));
        GivenWinnings(Winner(1, PrizeType.Round, "Ada L", 7m, "user-1", roundNumber: 2));

        var result = await HandleAsync();

        result.RoundPrizes.Should().HaveCount(3);
        result.RoundPrizes.Select(p => p.Name).Should().Equal("1", "2", "3");

        var unwon = result.RoundPrizes.Where(p => p.Winner == null).ToList();
        unwon.Should().HaveCount(2);
        unwon.Should().OnlyContain(p => p.Amount == 5m && p.UserId == null);
    }

    // The won round keeps the amount actually paid, which can differ from the configured amount when a
    // prize was shared or rolled over.
    [Fact]
    public async Task Handle_ShouldKeepTheAmountActuallyPaid_ForAWonRound()
    {
        GivenLeague(totalRoundsInSeason: 2);
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));
        GivenWinnings(Winner(1, PrizeType.Round, "Ada L", 7.5m, "user-1", roundNumber: 1));

        var result = await HandleAsync();

        var won = result.RoundPrizes.Single(p => p.Name == "1");
        won.Amount.Should().Be(7.5m);
        won.Winner.Should().Be("Ada L");
        won.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Handle_ShouldOrderRoundPrizesNumerically_NotAsText()
    {
        GivenLeague(totalRoundsInSeason: 12);
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));
        GivenWinnings(Winner(1, PrizeType.Round, "Ada L", 5m, "user-1", roundNumber: 10));

        var result = await HandleAsync();

        // Text ordering would put "10" between "1" and "2".
        result.RoundPrizes.Select(p => int.Parse(p.Name)).Should().BeInAscendingOrder();
        result.RoundPrizes.Select(p => p.Name).Should().Equal(
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12");
    }

    [Fact]
    public async Task Handle_ShouldLeaveRoundPrizesEmpty_WhenTheLeagueHasNoRoundPrize()
    {
        GivenLeague(totalRoundsInSeason: 3);
        GivenPrizeSettings(Setting(1, PrizeType.Overall, "Champion", 50m));

        var result = await HandleAsync();

        result.RoundPrizes.Should().BeEmpty();
    }

    // ---------- monthly prizes ----------

    // A season runs August to May, so the months wrap into the next calendar year. Ordering them by
    // month number alone would put January before August; the handler shifts the earlier months into
    // the following year first.
    [Fact]
    public async Task Handle_ShouldOrderMonthlyPrizesAcrossTheYearBoundary()
    {
        GivenLeague(
            seasonStartUtc: new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            seasonEndUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        GivenPrizeSettings(Setting(1, PrizeType.Monthly, "Monthly", 15m));

        var result = await HandleAsync();

        result.MonthlyPrizes.Select(p => p.Name).Should().Equal(
            "August", "September", "October", "November", "December", "January");
    }

    [Fact]
    public async Task Handle_ShouldPadEveryUnwonMonth_WithTheConfiguredAmount()
    {
        GivenLeague(
            seasonStartUtc: new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            seasonEndUtc: new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        GivenPrizeSettings(Setting(1, PrizeType.Monthly, "Monthly", 15m));
        GivenWinnings(Winner(1, PrizeType.Monthly, "Ada L", 20m, "user-1", month: 9));

        var result = await HandleAsync();

        result.MonthlyPrizes.Select(p => p.Name).Should().Equal("August", "September", "October");
        result.MonthlyPrizes.Single(p => p.Name == "September").Winner.Should().Be("Ada L");
        result.MonthlyPrizes.Where(p => p.Winner == null).Should().OnlyContain(p => p.Amount == 15m);
    }

    [Fact]
    public async Task Handle_ShouldLeaveMonthlyPrizesEmpty_WhenTheLeagueHasNoMonthlyPrize()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));

        var result = await HandleAsync();

        result.MonthlyPrizes.Should().BeEmpty();
    }

    // ---------- stage prizes ----------

    [Fact]
    public async Task Handle_ShouldListAStagePrizeAsUnwon_WhenNobodyHasWonIt()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(1, PrizeType.Stages, "Group Stage", 25m, stage: "GroupStage"));

        var result = await HandleAsync();

        result.StagePrizes.Should().HaveCount(1);
        result.StagePrizes[0].Name.Should().Be("Group Stage");
        result.StagePrizes[0].Amount.Should().Be(25m);
        result.StagePrizes[0].Winner.Should().BeNull();
    }

    // A shared stage prize produces one row per winner rather than one row for the prize.
    [Fact]
    public async Task Handle_ShouldListOneRowPerWinner_WhenAStagePrizeIsShared()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(1, PrizeType.Stages, "Group Stage", 25m, stage: "GroupStage"));
        GivenWinnings(
            Winner(1, PrizeType.Stages, "Ada L", 12.5m, "user-1"),
            Winner(1, PrizeType.Stages, "Grace H", 12.5m, "user-2"));

        var result = await HandleAsync();

        result.StagePrizes.Should().HaveCount(2);
        result.StagePrizes.Select(p => p.Winner).Should().BeEquivalentTo("Ada L", "Grace H");
        result.StagePrizes.Should().OnlyContain(p => p.Amount == 12.5m);
    }

    // ---------- end-of-season prizes ----------

    [Fact]
    public async Task Handle_ShouldCollectEveryPrizeThatIsNotRoundMonthlyOrStage()
    {
        GivenLeague();
        GivenPrizeSettings(
            Setting(1, PrizeType.Round, "Round", 5m),
            Setting(2, PrizeType.Monthly, "Monthly", 15m),
            Setting(3, PrizeType.Stages, "Group Stage", 25m, stage: "GroupStage"),
            Setting(4, PrizeType.Overall, "Champion", 100m),
            Setting(5, PrizeType.MostExactScores, "Most Exact Scores", 40m));

        var result = await HandleAsync();

        result.EndOfSeasonPrizes.Select(p => p.Name)
            .Should().BeEquivalentTo("Champion", "Most Exact Scores");
    }

    [Fact]
    public async Task Handle_ShouldAttributeAnEndOfSeasonPrize_ToItsWinner()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(4, PrizeType.Overall, "Champion", 100m));
        GivenWinnings(Winner(4, PrizeType.Overall, "Ada L", 100m, "user-1"));

        var result = await HandleAsync();

        result.EndOfSeasonPrizes.Should().HaveCount(1);
        result.EndOfSeasonPrizes[0].Winner.Should().Be("Ada L");
        result.EndOfSeasonPrizes[0].UserId.Should().Be("user-1");
    }

    // ---------- leaderboard aggregation ----------

    [Fact]
    public async Task Handle_ShouldSplitEachMembersWinningsByPrizeType()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));
        GivenMembers(new LeagueMemberQueryResult("Ada L", "user-1"));
        GivenWinnings(
            Winner(1, PrizeType.Round, "Ada L", 5m, "user-1", roundNumber: 1),
            Winner(2, PrizeType.Monthly, "Ada L", 15m, "user-1", month: 9),
            Winner(3, PrizeType.Stages, "Ada L", 25m, "user-1"),
            Winner(4, PrizeType.Overall, "Ada L", 100m, "user-1"));

        var result = await HandleAsync();

        var entry = result.Leaderboard.Entries.Single();
        entry.RoundWinnings.Should().Be(5m);
        entry.MonthlyWinnings.Should().Be(15m);
        entry.StageWinnings.Should().Be(25m);
        entry.EndOfSeasonWinnings.Should().Be(100m);
        entry.TotalWinnings.Should().Be(145m);
    }

    // Every approved member appears, including those who have won nothing, so the table doubles as the
    // league's member list.
    [Fact]
    public async Task Handle_ShouldIncludeAMemberWhoHasWonNothing()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));
        GivenMembers(
            new LeagueMemberQueryResult("Ada L", "user-1"),
            new LeagueMemberQueryResult("Grace H", "user-2"));
        GivenWinnings(Winner(1, PrizeType.Round, "Ada L", 5m, "user-1", roundNumber: 1));

        var result = await HandleAsync();

        result.Leaderboard.Entries.Should().HaveCount(2);
        result.Leaderboard.Entries.Single(e => e.UserId == "user-2").TotalWinnings.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldOrderTheLeaderboardByTotalWinningsThenName()
    {
        GivenLeague();
        GivenPrizeSettings(Setting(1, PrizeType.Round, "Round", 5m));
        GivenMembers(
            new LeagueMemberQueryResult("Zoe W", "user-3"),
            new LeagueMemberQueryResult("Ada L", "user-1"),
            new LeagueMemberQueryResult("Grace H", "user-2"));
        GivenWinnings(
            Winner(1, PrizeType.Round, "Ada L", 5m, "user-1", roundNumber: 1),
            Winner(1, PrizeType.Round, "Grace H", 20m, "user-2", roundNumber: 2));

        var result = await HandleAsync();

        // Grace 20, Ada 5, then the two on nothing alphabetically - Zoe last.
        result.Leaderboard.Entries.Select(e => e.PlayerName)
            .Should().Equal("Grace H", "Ada L", "Zoe W");
    }
}
