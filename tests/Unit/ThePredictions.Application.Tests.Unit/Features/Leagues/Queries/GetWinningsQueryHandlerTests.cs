using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's winnings page: every prize on offer, who has won each, and what each member has taken.
///
/// The page pads its lists with the prizes nobody has won yet, so most of these tests are about what appears when nothing
/// has happened - which is the state a league spends most of its season in.
/// </summary>
public class GetWinningsQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private static readonly DateTime Now = new(2027, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc);

    private readonly IWinningsQuery _winningsQuery = Substitute.For<IWinningsQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetWinningsQueryHandler _handler;

    public GetWinningsQueryHandlerTests()
    {
        _handler = new GetWinningsQueryHandler(_winningsQuery, _membershipService, new TestDateTimeProvider(Now));
    }

    #region Whether there is anything to show

    [Fact]
    public async Task Handle_ShouldEnforceMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _winningsQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyResult_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _winningsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((WinningsData?)null);

        // Act
        var winnings = await HandleAsync();

        // Assert - not an exception: this page is reachable for a league that has just been deleted.
        winnings.WinningsCalculated.Should().BeFalse();
        winnings.EntryCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportWinningsAsNotCalculated_WhenTheEntryDeadlineHasNotPassed()
    {
        // Arrange
        Given(
            Header(entryDeadlineUtc: Now.AddDays(1), entryCount: 10, entryCost: 5m),
            settings: [Setting(1, PrizeType.Round, 5m)]);

        // Act
        var winnings = await HandleAsync();

        // Assert - who is competing is still changing, so the pot is all there is to show.
        winnings.WinningsCalculated.Should().BeFalse();
        winnings.TotalPrizePot.Should().Be(50m);
        winnings.RoundPrizes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReportWinningsAsNotCalculated_WhenTheLeagueHasNoPrizeSettings()
    {
        // Arrange
        Given(Header(entryDeadlineUtc: Now.AddDays(-1)));

        // Act
        var winnings = await HandleAsync();

        // Assert - nothing to win.
        winnings.WinningsCalculated.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportWinningsAsCalculated_OnceTheDeadlineHasPassedAndPrizesExist()
    {
        // Arrange
        Given(
            Header(entryDeadlineUtc: Now.AddDays(-1)),
            settings: [Setting(1, PrizeType.Round, 5m)]);

        // Act
        var winnings = await HandleAsync();

        // Assert
        winnings.WinningsCalculated.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldTreatALeagueWithNoDeadlineAsClosed()
    {
        // Arrange - the column allows null, and the old comparison against a non-nullable field would have failed to
        // materialise rather than reaching this decision.
        Given(
            Header(entryDeadlineUtc: null),
            settings: [Setting(1, PrizeType.Round, 5m)]);

        // Act
        var winnings = await HandleAsync();

        // Assert
        winnings.WinningsCalculated.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldWorkOutThePotFromTheEntriesAlone()
    {
        // Arrange - a league with an administrator top-up as well as entry fees.
        Given(Header(entryCount: 10, entryCost: 5m, prizeFundOverride: 100m));

        // Act
        var winnings = await HandleAsync();

        // Assert - preserved: this is the one page that leaves the top-up out of the pot. Flagged in the plan document.
        winnings.TotalPrizePot.Should().Be(50m);
    }

    #endregion

    #region Round prizes

    [Fact]
    public async Task Handle_ShouldPadEveryUnwonRound_WithTheConfiguredAmount()
    {
        // Arrange
        Given(
            Header(totalRoundsInSeason: 3),
            settings: [Setting(1, PrizeType.Round, 5m)]);

        // Act
        var winnings = await HandleAsync();

        // Assert - a league mid-season is mostly unwon prizes, and the page shows every round.
        winnings.RoundPrizes.Should().HaveCount(3);
        winnings.RoundPrizes.Should().OnlyContain(prize => prize.Amount == 5m && prize.Winner == null);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheAmountActuallyPaid_ForAWonRound()
    {
        // Arrange - the prize was 5, but 2.50 was paid because it was shared.
        Given(
            Header(totalRoundsInSeason: 2),
            settings: [Setting(1, PrizeType.Round, 5m)],
            winnings: [Won(1, PrizeType.Round, 2.50m, "Ada", "Lovelace", roundNumber: 1)]);

        // Act
        var prizes = (await HandleAsync()).RoundPrizes;

        // Assert
        prizes.Single(prize => prize.Name == "1").Amount.Should().Be(2.50m);
        prizes.Single(prize => prize.Name == "1").Winner.Should().Be("Ada L");
        prizes.Single(prize => prize.Name == "2").Amount.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_ShouldOrderRoundPrizesNumerically_NotAsText()
    {
        // Arrange
        Given(
            Header(totalRoundsInSeason: 12),
            settings: [Setting(1, PrizeType.Round, 5m)]);

        // Act
        var prizes = (await HandleAsync()).RoundPrizes;

        // Assert - round 2 before round 10, which sorting the names as strings would get wrong.
        prizes.Select(prize => prize.Name).Should().Equal("1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12");
    }

    [Fact]
    public async Task Handle_ShouldLeaveRoundPrizesEmpty_WhenTheLeagueHasNoRoundPrize()
    {
        // Arrange
        Given(
            Header(totalRoundsInSeason: 10),
            settings: [Setting(1, PrizeType.Overall, 100m)]);

        // Act
        var winnings = await HandleAsync();

        // Assert - no round prize configured means no rounds listed, rather than ten rows worth nothing.
        winnings.RoundPrizes.Should().BeEmpty();
    }

    #endregion

    #region Monthly prizes

    [Fact]
    public async Task Handle_ShouldOrderMonthlyPrizesAcrossTheYearBoundary()
    {
        // Arrange - an August-to-May season.
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Monthly, 20m)]);

        // Act
        var prizes = (await HandleAsync()).MonthlyPrizes;

        // Assert - the season's order, so January follows December rather than opening the list.
        prizes.Select(prize => prize.Name).Should().Equal(
            "August", "September", "October", "November", "December",
            "January", "February", "March", "April", "May");
    }

    [Fact]
    public async Task Handle_ShouldNameMonthsInEnglish()
    {
        // Arrange - the old code formatted these with the machine's locale and then parsed the name back to sort by it.
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Monthly, 20m)]);

        // Act
        var prizes = (await HandleAsync()).MonthlyPrizes;

        // Assert
        prizes.First().Name.Should().Be("August");
    }

    [Fact]
    public async Task Handle_ShouldPadEveryUnwonMonth_WithTheConfiguredAmount()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Monthly, 20m)],
            winnings: [Won(1, PrizeType.Monthly, 20m, "Ada", "Lovelace", month: 9)]);

        // Act
        var prizes = (await HandleAsync()).MonthlyPrizes;

        // Assert
        prizes.Single(prize => prize.Name == "September").Winner.Should().Be("Ada L");
        prizes.Where(prize => prize.Winner == null).Should().OnlyContain(prize => prize.Amount == 20m);
        prizes.Should().HaveCount(10);
    }

    [Fact]
    public async Task Handle_ShouldLeaveMonthlyPrizesEmpty_WhenTheLeagueHasNoMonthlyPrize()
    {
        // Arrange
        Given(Header(), settings: [Setting(1, PrizeType.Overall, 100m)]);

        // Act
        var winnings = await HandleAsync();

        // Assert
        winnings.MonthlyPrizes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOfferEachMonthOnce_ForASeasonLongerThanAYear()
    {
        // Arrange - not reachable with a real season, and the old code would have listed the repeat twice.
        Given(
            Header(seasonEndDateUtc: SeasonStart.AddMonths(13)),
            settings: [Setting(1, PrizeType.Monthly, 20m)]);

        // Act
        var prizes = (await HandleAsync()).MonthlyPrizes;

        // Assert
        prizes.Select(prize => prize.Name).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Stage and end-of-season prizes

    [Fact]
    public async Task Handle_ShouldListAStagePrizeAsUnwon_WhenNobodyHasWonIt()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Stages, 40m, name: "Group Stage", stage: "Group Stage")]);

        // Act
        var prize = (await HandleAsync()).StagePrizes.Single();

        // Assert
        prize.Name.Should().Be("Group Stage");
        prize.Amount.Should().Be(40m);
        prize.Winner.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldListOneRowPerWinner_WhenAStagePrizeIsShared()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Stages, 40m, name: "Group Stage", stage: "Group Stage")],
            winnings:
            [
                Won(1, PrizeType.Stages, 20m, "Ada", "Lovelace"),
                Won(1, PrizeType.Stages, 20m, "Grace", "Hopper")
            ]);

        // Act
        var prizes = (await HandleAsync()).StagePrizes;

        // Assert - a shared prize is split, so each winner gets their own line with their own amount.
        prizes.Should().HaveCount(2);
        prizes.Select(prize => prize.Winner).Should().BeEquivalentTo(["Ada L", "Grace H"]);
        prizes.Should().OnlyContain(prize => prize.Amount == 20m);
    }

    [Fact]
    public async Task Handle_ShouldCollectEveryPrizeThatIsNotRoundMonthlyOrStage()
    {
        // Arrange
        Given(
            Header(),
            settings:
            [
                Setting(1, PrizeType.Round, 5m),
                Setting(2, PrizeType.Monthly, 20m),
                Setting(3, PrizeType.Stages, 40m, stage: "Group Stage"),
                Setting(4, PrizeType.Overall, 100m, name: "1st Place"),
                Setting(5, PrizeType.MostExactScores, 30m, name: "Most Exact Scores")
            ]);

        // Act
        var prizes = (await HandleAsync()).EndOfSeasonPrizes;

        // Assert
        prizes.Select(prize => prize.Name).Should().BeEquivalentTo(["1st Place", "Most Exact Scores"]);
    }

    [Fact]
    public async Task Handle_ShouldAttributeAnEndOfSeasonPrize_ToItsWinner()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(4, PrizeType.Overall, 100m, name: "1st Place")],
            winnings: [Won(4, PrizeType.Overall, 100m, "Ada", "Lovelace")]);

        // Act
        var prize = (await HandleAsync()).EndOfSeasonPrizes.Single();

        // Assert
        prize.Winner.Should().Be("Ada L");
        prize.Amount.Should().Be(100m);
    }

    #endregion

    [Fact]
    public async Task Handle_ShouldSkipARoundPrizeRecordedWithoutARound()
    {
        // Arrange - a state that should not exist. The old code named the line with an empty string and then took the whole
        // page down parsing it back to sort by it.
        Given(
            Header(totalRoundsInSeason: 2),
            settings: [Setting(1, PrizeType.Round, 5m)],
            winnings: [Won(1, PrizeType.Round, 5m, "Ada", "Lovelace", roundNumber: null)]);

        // Act
        var prizes = (await HandleAsync()).RoundPrizes;

        // Assert - both rounds still listed as unwon; a missing line beats a page that will not load.
        prizes.Should().HaveCount(2);
        prizes.Should().OnlyContain(prize => prize.Winner == null);
    }

    [Fact]
    public async Task Handle_ShouldNotPutARoundWinInTheMonthlyList()
    {
        // Arrange - both kinds of prize on offer, and a round win recorded.
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Monthly, 20m), Setting(2, PrizeType.Round, 5m)],
            winnings: [Won(2, PrizeType.Round, 5m, "Ada", "Lovelace", roundNumber: 1)]);

        // Act
        var winnings = await HandleAsync();

        // Assert - every month still unwon, and the round win in its own list.
        winnings.MonthlyPrizes.Should().OnlyContain(prize => prize.Winner == null);
        winnings.RoundPrizes.Should().Contain(prize => prize.Winner == "Ada L");
    }

    [Fact]
    public async Task Handle_ShouldSkipAMonthlyPrizeRecordedWithoutAMonth()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Monthly, 20m)],
            winnings: [Won(1, PrizeType.Monthly, 20m, "Ada", "Lovelace", month: null)]);

        // Act
        var prizes = (await HandleAsync()).MonthlyPrizes;

        // Assert
        prizes.Should().OnlyContain(prize => prize.Winner == null);
    }

    #region The winnings leaderboard

    [Fact]
    public async Task Handle_ShouldSplitEachMembersWinningsByPrizeType()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Round, 5m)],
            winnings:
            [
                Won(1, PrizeType.Round, 5m, "Ada", "Lovelace", roundNumber: 1),
                Won(2, PrizeType.Monthly, 20m, "Ada", "Lovelace", month: 9),
                Won(3, PrizeType.Stages, 40m, "Ada", "Lovelace"),
                Won(4, PrizeType.Overall, 100m, "Ada", "Lovelace"),
                Won(5, PrizeType.MostExactScores, 30m, "Ada", "Lovelace")
            ],
            members: [Member("u1", "Ada", "Lovelace")]);

        // Act
        var entry = (await HandleAsync()).Leaderboard.Entries.Single();

        // Assert - the last two are both "end of season", which is everything that is not a round, month or stage.
        entry.RoundWinnings.Should().Be(5m);
        entry.MonthlyWinnings.Should().Be(20m);
        entry.StageWinnings.Should().Be(40m);
        entry.EndOfSeasonWinnings.Should().Be(130m);
        entry.TotalWinnings.Should().Be(195m);
    }

    [Fact]
    public async Task Handle_ShouldIncludeAMemberWhoHasWonNothing()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Round, 5m)],
            members: [Member("u1", "Ada", "Lovelace")]);

        // Act
        var entry = (await HandleAsync()).Leaderboard.Entries.Single();

        // Assert - the table is the league, not a list of winners.
        entry.PlayerName.Should().Be("Ada L");
        entry.TotalWinnings.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldOrderTheLeaderboardByTotalWinningsThenName()
    {
        // Arrange
        Given(
            Header(),
            settings: [Setting(1, PrizeType.Round, 5m)],
            winnings:
            [
                Won(1, PrizeType.Round, 50m, "Grace", "Hopper", roundNumber: 1, userId: "u2"),
                Won(1, PrizeType.Round, 50m, "Ada", "Lovelace", roundNumber: 2, userId: "u1"),
                Won(1, PrizeType.Round, 100m, "Alan", "Turing", roundNumber: 3, userId: "u3")
            ],
            members:
            [
                Member("u1", "Ada", "Lovelace"),
                Member("u2", "Grace", "Hopper"),
                Member("u3", "Alan", "Turing")
            ]);

        // Act
        var entries = (await HandleAsync()).Leaderboard.Entries;

        // Assert
        entries.Select(entry => entry.PlayerName).Should().Equal("Alan T", "Ada L", "Grace H");
    }

    #endregion

    private void Given(
        WinningsHeaderRow? header = null,
        IReadOnlyList<WinningsPrizeSettingRow>? settings = null,
        IReadOnlyList<WinningsRow>? winnings = null,
        IReadOnlyList<LeaderboardParticipantRow>? members = null)
    {
        _winningsQuery
            .ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(new WinningsData(header ?? Header(), settings ?? [], winnings ?? [], members ?? []));
    }

    private async Task<WinningsDto> HandleAsync() =>
        await _handler.Handle(new GetWinningsQuery(LeagueId, UserId), CancellationToken.None);

    /// <summary>
    /// A league whose entry deadline has passed by default - which for most of these tests is the interesting state,
    /// because it is the one where prizes have been worked out.
    /// </summary>
    /// <remarks>
    /// <paramref name="entryDeadlineUtc"/> passes straight through, so <c>null</c> means a league with no deadline at all
    /// rather than the default. An earlier version of this helper defaulted a null to the season start, which quietly made
    /// the "no deadline" test a "past deadline" test - the mutation check caught it.
    /// </remarks>
    private static WinningsHeaderRow Header(
        DateTime? entryDeadlineUtc = null,
        decimal entryCost = 10m,
        int entryCount = 5,
        decimal? prizeFundOverride = null,
        DateTime? seasonEndDateUtc = null,
        int totalRoundsInSeason = 38) =>
        new(
            entryDeadlineUtc,
            entryCost,
            entryCount,
            prizeFundOverride,
            SeasonStart,
            seasonEndDateUtc ?? SeasonEnd,
            totalRoundsInSeason);

    private static WinningsPrizeSettingRow Setting(
        int id,
        PrizeType prizeType,
        decimal amount,
        string? name = null,
        string? stage = null) =>
        new(id, prizeType, name ?? prizeType.ToString(), amount, stage);

    private static WinningsRow Won(
        int settingId,
        PrizeType prizeType,
        decimal amount,
        string firstName,
        string lastName,
        int? roundNumber = null,
        int? month = null,
        string userId = "u1") =>
        new(amount, settingId, prizeType, firstName, lastName, roundNumber, month, userId);

    private static LeaderboardParticipantRow Member(string userId, string firstName, string lastName) =>
        new(userId, firstName, lastName);
}
