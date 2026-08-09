using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;
using static ThePredictions.Application.Features.External.Tasks.Queries.GetLeagueWelcomeBatchQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.External.Tasks.Queries;

/// <summary>
/// Assembles the hourly league-welcome email batch. The read returns one flat row per
/// (league, member) pair, and this handler folds that back into one league carrying its members,
/// prizes and boosts - so a league with eight new members produces one league with eight recipients,
/// not eight leagues. Getting that wrong sends the same person several copies, which is the failure
/// this batch exists to avoid.
/// </summary>
public class GetLeagueWelcomeBatchQueryHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonStart = new(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly GetLeagueWelcomeBatchQueryHandler _handler;

    public GetLeagueWelcomeBatchQueryHandlerTests()
    {
        _handler = new GetLeagueWelcomeBatchQueryHandler(_dbConnection);
    }

    private static LeagueRecipientRow Recipient(
        int leagueId, string userId, string leagueName = "The Office",
        DateTime? seasonStartUtc = null, DateTime? seasonEndUtc = null,
        bool hasPrizes = true, int memberCount = 8, int numberOfRounds = 38) =>
        new(leagueId, leagueName, "2026/27", hasPrizes, numberOfRounds,
            seasonStartUtc ?? SeasonStart, seasonEndUtc ?? SeasonEnd, memberCount,
            userId, $"{userId}@example.com", $"Name {userId}");

    private void GivenRecipients(params LeagueRecipientRow[] rows) =>
        _dbConnection.QueryAsync<LeagueRecipientRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>()).Returns(rows);

    private void GivenPrizes(params PrizeRow[] rows) =>
        _dbConnection.QueryAsync<PrizeRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>()).Returns(rows);

    private void GivenBoosts(params BoostRow[] rows) =>
        _dbConnection.QueryAsync<BoostRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>()).Returns(rows);

    private void GivenBoostWindows(params WindowRow[] rows) =>
        _dbConnection.QueryAsync<WindowRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>()).Returns(rows);

    private Task<IReadOnlyList<LeagueWelcomeLeague>> HandleAsync() =>
        _handler.Handle(new GetLeagueWelcomeBatchQuery(NowUtc, NowUtc.AddDays(-7)), CancellationToken.None);

    // ---------- nothing to send ----------

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenNobodyIsAwaitingAWelcome()
    {
        GivenRecipients();

        (await HandleAsync()).Should().BeEmpty();
    }

    // The follow-up reads are keyed by the league ids found in the first, so an empty batch must stop
    // rather than query for prizes with an empty list.
    [Fact]
    public async Task Handle_ShouldNotReadPrizesOrBoosts_WhenNobodyIsAwaitingAWelcome()
    {
        GivenRecipients();

        await HandleAsync();

        await _dbConnection.DidNotReceiveWithAnyArgs().QueryAsync<PrizeRow>(default!, CancellationToken.None, default);
        await _dbConnection.DidNotReceiveWithAnyArgs().QueryAsync<BoostRow>(default!, CancellationToken.None, default);
    }

    // ---------- folding rows back into leagues ----------

    [Fact]
    public async Task Handle_ShouldProduceOneLeagueWithEveryRecipient_WhenALeagueHasSeveralNewMembers()
    {
        GivenRecipients(Recipient(1, "user-1"), Recipient(1, "user-2"), Recipient(1, "user-3"));

        var leagues = await HandleAsync();

        leagues.Should().ContainSingle();
        leagues[0].Recipients.Select(r => r.UserId).Should().Equal("user-1", "user-2", "user-3");
    }

    [Fact]
    public async Task Handle_ShouldKeepEachLeaguesRecipientsToItself()
    {
        GivenRecipients(Recipient(1, "user-1"), Recipient(2, "user-2"), Recipient(1, "user-3"));

        var leagues = await HandleAsync();

        leagues.Should().HaveCount(2);
        leagues.Single(l => l.LeagueId == 1).Recipients.Should().HaveCount(2);
        leagues.Single(l => l.LeagueId == 2).Recipients.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheRecipientContactDetails()
    {
        GivenRecipients(Recipient(1, "user-1"));

        var recipient = (await HandleAsync())[0].Recipients.Single();

        recipient.Email.Should().Be("user-1@example.com");
        recipient.FirstName.Should().Be("Name user-1");
    }

    [Fact]
    public async Task Handle_ShouldCarryTheLeagueDetails()
    {
        GivenRecipients(Recipient(1, "user-1", leagueName: "The Office", hasPrizes: true, memberCount: 12, numberOfRounds: 38));

        var league = (await HandleAsync())[0];

        league.LeagueName.Should().Be("The Office");
        league.SeasonName.Should().Be("2026/27");
        league.HasPrizes.Should().BeTrue();
        league.MemberCount.Should().Be(12);
        league.NumberOfRounds.Should().Be(38);
    }

    // ---------- how many months the season spans ----------

    // A season runs August to May, so the count has to cross a calendar year rather than subtracting
    // month numbers. Both ends are included: August to May inclusive is ten months.
    [Fact]
    public async Task Handle_ShouldCountTheSeasonMonthsAcrossTheYearBoundary()
    {
        GivenRecipients(Recipient(1, "user-1",
            seasonStartUtc: new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            seasonEndUtc: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));

        (await HandleAsync())[0].NumberOfMonths.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldCountASingleMonth_WhenTheSeasonStartsAndEndsInOne()
    {
        GivenRecipients(Recipient(1, "user-1",
            seasonStartUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            seasonEndUtc: new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc)));

        (await HandleAsync())[0].NumberOfMonths.Should().Be(1);
    }

    // ---------- prizes and boosts ----------

    [Fact]
    public async Task Handle_ShouldAttachOnlyTheLeaguesOwnPrizes()
    {
        GivenRecipients(Recipient(1, "user-1"), Recipient(2, "user-2"));
        GivenPrizes(
            new PrizeRow(1, PrizeType.Overall, 1, null, 100m),
            new PrizeRow(2, PrizeType.Round, 1, null, 5m));

        var leagues = await HandleAsync();

        leagues.Single(l => l.LeagueId == 1).Prizes.Should().ContainSingle().Which.Amount.Should().Be(100m);
        leagues.Single(l => l.LeagueId == 2).Prizes.Should().ContainSingle().Which.Amount.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_ShouldLeavePrizesEmpty_WhenTheLeagueHasNone()
    {
        GivenRecipients(Recipient(1, "user-1", hasPrizes: false));

        (await HandleAsync())[0].Prizes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAttachEachBoostsOwnWindows_InRoundOrder()
    {
        GivenRecipients(Recipient(1, "user-1"));
        GivenBoosts(new BoostRow(7, 1, "Double Up", "Doubles your points", "https://example.test/b.png", 2));
        GivenBoostWindows(
            new WindowRow(7, 20, 38, 1),
            new WindowRow(7, 1, 19, 1),
            new WindowRow(99, 1, 38, 5));

        var boost = (await HandleAsync())[0].Boosts.Single();

        boost.Name.Should().Be("Double Up");
        boost.TotalUsesPerSeason.Should().Be(2);
        boost.Windows.Select(w => w.StartRoundNumber).Should().Equal(1, 20);
    }

    [Fact]
    public async Task Handle_ShouldLeaveBoostWindowsEmpty_WhenTheBoostRunsAllSeason()
    {
        GivenRecipients(Recipient(1, "user-1"));
        GivenBoosts(new BoostRow(7, 1, "Double Up", null, null, 2));

        (await HandleAsync())[0].Boosts.Single().Windows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLeaveBoostsEmpty_WhenTheLeagueHasNone()
    {
        GivenRecipients(Recipient(1, "user-1"));

        (await HandleAsync())[0].Boosts.Should().BeEmpty();
    }
}
