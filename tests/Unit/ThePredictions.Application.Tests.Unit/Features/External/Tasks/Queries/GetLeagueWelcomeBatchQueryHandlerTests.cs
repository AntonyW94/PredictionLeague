using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.External.Tasks.Queries;

/// <summary>
/// Assembles the hourly league-welcome email batch: for each league that has just closed to entry, everybody in it who has not
/// been welcomed yet, and what the league offers them.
/// </summary>
/// <remarks>
/// Two of these rules used to live four levels deep in a statement nothing could test, and both decide whether a real message
/// reaches a real player: the sent-log check that stops somebody being welcomed twice, and the guard that holds back a league
/// whose prizes have not been worked out from its scheme yet.
/// </remarks>
public class GetLeagueWelcomeBatchQueryHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonStart = new(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueWelcomeBatchQuery _welcomeBatchQuery = Substitute.For<ILeagueWelcomeBatchQuery>();
    private readonly GetLeagueWelcomeBatchQueryHandler _handler;

    private List<WelcomeLeagueRow> _leagues = [];
    private List<WelcomeRecipientRow> _recipients = [];
    private List<WelcomeNotificationRow> _alreadyNotified = [];
    private List<WelcomeSchemeRow> _schemes = [];
    private List<WelcomePrizeRow> _prizes = [];
    private List<WelcomeBoostRow> _boosts = [];
    private List<WelcomeBoostWindowRow> _boostWindows = [];

    public GetLeagueWelcomeBatchQueryHandlerTests()
    {
        _handler = new GetLeagueWelcomeBatchQueryHandler(_welcomeBatchQuery);
    }

    #region Who gets welcomed

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenNoLeagueHasJustClosed()
    {
        // Arrange
        GivenLeagues();

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotWelcomeSomebodyTwice()
    {
        // The one failure this job has to avoid. It was a NOT EXISTS against the sent-log inside the read.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"), Recipient(1, "user-2"));
        GivenAlreadyNotified(new WelcomeNotificationRow(1, "user-1"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Single().Recipients.Select(recipient => recipient.UserId).Should().Equal("user-2");
    }

    [Fact]
    public async Task Handle_ShouldStillWelcomeSomebodyWelcomedToAnotherLeague()
    {
        // Arrange - the sent-log is per league and per player, because one player can be in several.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenAlreadyNotified(new WelcomeNotificationRow(2, "user-1"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Single().Recipients.Select(recipient => recipient.UserId).Should().Equal("user-1");
    }

    [Fact]
    public async Task Handle_ShouldDropALeagueWhoseMembersHaveAllBeenWelcomed()
    {
        // Arrange - no recipients means no email, so the league itself is not worth returning.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenAlreadyNotified(new WelcomeNotificationRow(1, "user-1"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldKeepEachLeaguesRecipientsToItself()
    {
        // Arrange
        GivenLeagues(League(1), League(2));
        GivenRecipients(Recipient(1, "user-1"), Recipient(2, "user-2"), Recipient(1, "user-3"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Single(league => league.LeagueId == 1).Recipients.Should().HaveCount(2);
        leagues.Single(league => league.LeagueId == 2).Recipients.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheRecipientContactDetails()
    {
        // Arrange
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var recipient = (await HandleAsync()).Single().Recipients.Single();

        // Assert
        recipient.Email.Should().Be("user-1@example.com");
        recipient.FirstName.Should().Be("Ada");
    }

    [Fact]
    public async Task Handle_ShouldStillWelcomeSomebodyWithNoFirstName()
    {
        // Arrange - the column allows it, and a greeting with a blank in it is better than an email nobody gets.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1") with { FirstName = null });

        // Act
        var recipient = (await HandleAsync()).Single().Recipients.Single();

        // Assert
        recipient.FirstName.Should().BeEmpty();
    }

    #endregion

    #region A league whose prizes are not settled yet

    [Fact]
    public async Task Handle_ShouldHoldBackALeagueWhoseSchemeHasNotBeenWorkedOutIntoPrizes()
    {
        // Welcoming them would send an email about prizes with nothing in the list. The next hourly run picks the league up
        // once the freeze has happened.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenSchemes(new WelcomeSchemeRow(1));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldWelcomeALeagueWhoseSchemeHasBeenWorkedOutIntoPrizes()
    {
        // Arrange
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenSchemes(new WelcomeSchemeRow(1));
        GivenPrizes(new WelcomePrizeRow(1, PrizeType.Overall, 1, null, 100m));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldWelcomeALeagueWithNoSchemeAtAll()
    {
        // Arrange - a free, leaderboard-only league has nothing to freeze, so there is nothing to wait for.
        GivenLeagues(League(1) with { HasPrizes = false });
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldJudgeEachLeaguesSchemeOnItsOwn()
    {
        // Arrange - league 2's prizes are settled, league 1's are not; the ids run the other way to the outcome.
        GivenLeagues(League(1), League(2));
        GivenRecipients(Recipient(1, "user-1"), Recipient(2, "user-2"));
        GivenSchemes(new WelcomeSchemeRow(1), new WelcomeSchemeRow(2));
        GivenPrizes(new WelcomePrizeRow(2, PrizeType.Overall, 1, null, 100m));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Select(league => league.LeagueId).Should().Equal(2);
    }

    #endregion

    #region Order

    [Fact]
    public async Task Handle_ShouldReturnTheLeaguesInAStableOrder()
    {
        // Arrange - the read makes no promise about order, and the batch is easier to follow in the logs with one.
        GivenLeagues(League(3), League(1), League(2));
        GivenRecipients(Recipient(3, "user-3"), Recipient(1, "user-1"), Recipient(2, "user-2"));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Select(league => league.LeagueId).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldListEachLeaguesRecipientsByFirstName()
    {
        // Arrange - the ids run the other way to the names.
        GivenLeagues(League(1));
        GivenRecipients(
            Recipient(1, "user-1") with { FirstName = "Zara" },
            Recipient(1, "user-2") with { FirstName = "Ada" });

        // Act
        var recipients = (await HandleAsync()).Single().Recipients;

        // Assert
        recipients.Select(recipient => recipient.FirstName).Should().Equal("Ada", "Zara");
    }

    [Fact]
    public async Task Handle_ShouldSeparateTwoRecipientsWithTheSameFirstName()
    {
        // Arrange - two people called Ada must not swap places between runs.
        GivenLeagues(League(1));
        GivenRecipients(
            Recipient(1, "user-2") with { FirstName = "Ada" },
            Recipient(1, "user-1") with { FirstName = "Ada" });

        // Act
        var recipients = (await HandleAsync()).Single().Recipients;

        // Assert
        recipients.Select(recipient => recipient.UserId).Should().Equal("user-1", "user-2");
    }

    #endregion

    #region What the league offers

    [Fact]
    public async Task Handle_ShouldCarryTheLeagueDetails()
    {
        // Arrange
        GivenLeagues(League(1) with { LeagueName = "The Office", MemberCount = 12, NumberOfRounds = 38, HasPrizes = true });
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.LeagueName.Should().Be("The Office");
        league.SeasonName.Should().Be("2026/27");
        league.HasPrizes.Should().BeTrue();
        league.MemberCount.Should().Be(12);
        league.NumberOfRounds.Should().Be(38);
    }

    [Fact]
    public async Task Handle_ShouldAttachOnlyTheLeaguesOwnPrizes()
    {
        // Arrange
        GivenLeagues(League(1), League(2));
        GivenRecipients(Recipient(1, "user-1"), Recipient(2, "user-2"));
        GivenPrizes(
            new WelcomePrizeRow(1, PrizeType.Overall, 1, null, 100m),
            new WelcomePrizeRow(2, PrizeType.Round, 1, null, 5m));

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Single(league => league.LeagueId == 1).Prizes.Should().ContainSingle().Which.Amount.Should().Be(100m);
        leagues.Single(league => league.LeagueId == 2).Prizes.Should().ContainSingle().Which.Amount.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_ShouldCarryEachPrizesWordingAndPosition()
    {
        // Arrange
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenPrizes(new WelcomePrizeRow(1, PrizeType.Stages, 2, "Quarter Finals", 25m));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.PrizeType.Should().Be(PrizeType.Stages);
        prize.Rank.Should().Be(2);
        prize.Stage.Should().Be("Quarter Finals");
        prize.Amount.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_ShouldLeavePrizesEmpty_WhenTheLeagueHasNone()
    {
        // Arrange
        GivenLeagues(League(1) with { HasPrizes = false });
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.Prizes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotAdvertiseABoostTheLeagueHasSwitchedOff()
    {
        // The email tells somebody what they can do this season, and a disabled rule is not something they can do.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenBoosts(
            Boost(ruleId: 7, leagueId: 1) with { Name = "Double Up", IsEnabled = true },
            Boost(ruleId: 8, leagueId: 1) with { Name = "Banker", IsEnabled = false });

        // Act
        var boosts = (await HandleAsync()).Single().Boosts;

        // Assert
        boosts.Select(boost => boost.Name).Should().Equal("Double Up");
    }

    [Fact]
    public async Task Handle_ShouldNotShowTheWindowsOfABoostTheLeagueHasSwitchedOff()
    {
        // Arrange - a window belonging to a disabled rule used to have to be filtered out separately.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenBoosts(Boost(ruleId: 7, leagueId: 1) with { IsEnabled = false });
        GivenBoostWindows(new WelcomeBoostWindowRow(7, 1, 19, 1));

        // Act
        var boosts = (await HandleAsync()).Single().Boosts;

        // Assert
        boosts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAttachOnlyTheLeaguesOwnBoosts()
    {
        // Arrange
        GivenLeagues(League(1), League(2));
        GivenRecipients(Recipient(1, "user-1"), Recipient(2, "user-2"));
        GivenBoosts(
            Boost(ruleId: 7, leagueId: 1) with { Name = "Double Up" },
            Boost(ruleId: 8, leagueId: 2) with { Name = "Banker" });

        // Act
        var leagues = await HandleAsync();

        // Assert
        leagues.Single(league => league.LeagueId == 1).Boosts.Select(boost => boost.Name).Should().Equal("Double Up");
        leagues.Single(league => league.LeagueId == 2).Boosts.Select(boost => boost.Name).Should().Equal("Banker");
    }

    [Fact]
    public async Task Handle_ShouldCarryEachBoostsWordingAndSeasonCap()
    {
        // Arrange
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenBoosts(Boost(ruleId: 7, leagueId: 1) with
        {
            Name = "Double Up",
            Description = "Doubles your points",
            ImageUrl = "https://example.test/b.png",
            TotalUsesPerSeason = 2
        });

        // Act
        var boost = (await HandleAsync()).Single().Boosts.Single();

        // Assert
        boost.Name.Should().Be("Double Up");
        boost.Description.Should().Be("Doubles your points");
        boost.ImageUrl.Should().Be("https://example.test/b.png");
        boost.TotalUsesPerSeason.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldAttachEachBoostsOwnWindowsInRoundOrder()
    {
        // Arrange - the later window is listed first, and rule 99 belongs to nothing in this batch.
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenBoosts(Boost(ruleId: 7, leagueId: 1));
        GivenBoostWindows(
            new WelcomeBoostWindowRow(7, 20, 38, 1),
            new WelcomeBoostWindowRow(7, 1, 19, 2),
            new WelcomeBoostWindowRow(99, 1, 38, 5));

        // Act
        var windows = (await HandleAsync()).Single().Boosts.Single().Windows;

        // Assert
        windows.Select(window => window.StartRoundNumber).Should().Equal(1, 20);
        windows[0].EndRoundNumber.Should().Be(19);
        windows[0].MaxUsesInWindow.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldLeaveBoostWindowsEmpty_WhenTheBoostRunsAllSeason()
    {
        // Arrange
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));
        GivenBoosts(Boost(ruleId: 7, leagueId: 1));

        // Act
        var boost = (await HandleAsync()).Single().Boosts.Single();

        // Assert
        boost.Windows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLeaveBoostsEmpty_WhenTheLeagueHasNone()
    {
        // Arrange
        GivenLeagues(League(1));
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.Boosts.Should().BeEmpty();
    }

    #endregion

    #region How long the season runs

    [Fact]
    public async Task Handle_ShouldCountTheSeasonMonthsAcrossTheYearBoundary()
    {
        // August to May inclusive is ten months, which is why this is not a subtraction of month numbers.
        GivenLeagues(League(1) with
        {
            SeasonStartDateUtc = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEndDateUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.NumberOfMonths.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldCountASingleMonth_WhenTheSeasonStartsAndEndsInOne()
    {
        // Arrange
        GivenLeagues(League(1) with
        {
            SeasonStartDateUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEndDateUtc = new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc)
        });
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.NumberOfMonths.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldStepAMonthAtATimeFromTheSeasonsStartDay()
    {
        // Arrange - counted from the start day rather than by calendar month, so a season running from the 20th of August to
        // the 2nd of September is one month rather than two. Preserved as it was; a season starting mid-month is not a shape
        // the site has ever had.
        GivenLeagues(League(1) with
        {
            SeasonStartDateUtc = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            SeasonEndDateUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)
        });
        GivenRecipients(Recipient(1, "user-1"));

        // Act
        var league = (await HandleAsync()).Single();

        // Assert
        league.NumberOfMonths.Should().Be(1);
    }

    #endregion

    #region The window handed to the read

    [Fact]
    public async Task Handle_ShouldAskForTheWindowTheJobWasGiven()
    {
        // Arrange - the job passes its own clock in, and choosing which leagues have just closed stays in the read.
        var windowStartUtc = NowUtc.AddDays(-7);
        GivenLeagues();
        GivenTheRead();

        // Act
        await _handler.Handle(new GetLeagueWelcomeBatchQuery(NowUtc, windowStartUtc), CancellationToken.None);

        // Assert
        await _welcomeBatchQuery.Received(1).ExecuteAsync(windowStartUtc, NowUtc, Arg.Any<CancellationToken>());
    }

    #endregion

    private static WelcomeLeagueRow League(int leagueId) =>
        new(leagueId, $"League {leagueId}", "2026/27", HasPrizes: true, MemberCount: 8, NumberOfRounds: 38,
            SeasonStart, SeasonEnd);

    private static WelcomeRecipientRow Recipient(int leagueId, string userId) =>
        new(leagueId, userId, $"{userId}@example.com", "Ada");

    private static WelcomeBoostRow Boost(int ruleId, int leagueId) =>
        new(ruleId, leagueId, "Double Up", "Doubles your points", "https://example.test/b.png",
            TotalUsesPerSeason: 2, IsEnabled: true);

    private void GivenLeagues(params WelcomeLeagueRow[] leagues) => _leagues = [.. leagues];

    private void GivenRecipients(params WelcomeRecipientRow[] recipients) => _recipients = [.. recipients];

    private void GivenAlreadyNotified(params WelcomeNotificationRow[] notifications) => _alreadyNotified = [.. notifications];

    private void GivenSchemes(params WelcomeSchemeRow[] schemes) => _schemes = [.. schemes];

    private void GivenPrizes(params WelcomePrizeRow[] prizes) => _prizes = [.. prizes];

    private void GivenBoosts(params WelcomeBoostRow[] boosts) => _boosts = [.. boosts];

    private void GivenBoostWindows(params WelcomeBoostWindowRow[] windows) => _boostWindows = [.. windows];

    private void GivenTheRead() =>
        _welcomeBatchQuery
            .ExecuteAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new LeagueWelcomeBatchData(
                _leagues, _recipients, _alreadyNotified, _schemes, _prizes, _boosts, _boostWindows));

    private Task<IReadOnlyList<LeagueWelcomeLeague>> HandleAsync()
    {
        GivenTheRead();

        return _handler.Handle(new GetLeagueWelcomeBatchQuery(NowUtc, NowUtc.AddDays(-7)), CancellationToken.None);
    }
}
