using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Queries;

/// <summary>
/// Everybody with a prize to be told about, grouped so each of them gets one email listing all of theirs.
/// </summary>
/// <remarks>
/// The rule worth the most here is matching a winning against the sent-log. The same prize slot pays out repeatedly - a round
/// prize once a round, a monthly prize once a month - so the match has to be on the scope as well as the slot. In SQL that
/// needed <c>ISNULL(..., -1)</c> on both sides of both comparisons, because there two nulls are never equal; getting it wrong
/// either emails somebody twice about the same win or never emails them at all.
/// </remarks>
public class GetPrizeWinnersForRoundQueryHandlerTests
{
    private const int RoundId = 42;

    private static readonly DateTime DeadlineUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly IRoundHeaderQuery _roundHeaderQuery = Substitute.For<IRoundHeaderQuery>();
    private readonly IPrizeWinnersQuery _prizeWinnersQuery = Substitute.For<IPrizeWinnersQuery>();
    private readonly GetPrizeWinnersForRoundQueryHandler _handler;

    private RoundHeaderRow? _round = Round(5, "Gameweek 5");
    private List<PrizeWinningRow> _winnings = [];
    private List<PrizeNotificationRow> _notifications = [];
    private List<SeasonRoundNameRow> _seasonRounds = [];

    public GetPrizeWinnersForRoundQueryHandlerTests()
    {
        _handler = new GetPrizeWinnersForRoundQueryHandler(_roundHeaderQuery, _prizeWinnersQuery);
    }

    #region Nothing to send

    [Fact]
    public async Task Handle_ShouldReturnNobody_WhenTheRoundDoesNotExist()
    {
        // Arrange
        _round = null;

        // Act
        var winners = await HandleAsync();

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotReadTheWinnings_WhenTheRoundDoesNotExist()
    {
        // Arrange - there is nothing to name the prizes after, so the second read is not worth making.
        _round = null;

        // Act
        await HandleAsync();

        // Assert
        await _prizeWinnersQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnNobody_WhenNothingWasWon()
    {
        // Arrange
        GivenWinnings();

        // Act
        var winners = await HandleAsync();

        // Assert
        winners.Should().BeEmpty();
    }

    #endregion

    #region A prize worth nothing

    [Fact]
    public async Task Handle_ShouldNotEmailSomebodyAboutAPrizeOfNothing()
    {
        // A winning of zero is recorded when somebody placed in a category that pays out nothing at that position. Emailing
        // them would be telling them they had won no money.
        GivenWinnings(Winning("user-1", amount: 0m));

        // Act
        var winners = await HandleAsync();

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldEmailSomebodyAboutTheirOtherPrizes_WhenOneOfThemIsWorthNothing()
    {
        // Arrange
        GivenWinnings(
            Winning("user-1", amount: 0m, leagueName: "Nothing League"),
            Winning("user-1", amount: 100m, leagueName: "Real League"));

        // Act
        var winner = (await HandleAsync()).Single();

        // Assert
        winner.Prizes.Select(prize => prize.LeagueName).Should().Equal("Real League");
    }

    [Fact]
    public async Task Handle_ShouldEmailSomebodyAboutAPrizeOfPennies()
    {
        // Arrange - the boundary is zero, not a pound.
        GivenWinnings(Winning("user-1", amount: 0.01m));

        // Act
        var winners = await HandleAsync();

        // Assert
        winners.Should().ContainSingle();
    }

    #endregion

    #region One email each

    [Fact]
    public async Task Handle_ShouldGroupOnePlayersPrizesIntoOneEmail()
    {
        // Arrange
        GivenWinnings(
            Winning("user-1", leagueName: "The Office"),
            Winning("user-1", leagueName: "The Pub"));

        // Act
        var winners = await HandleAsync();

        // Assert
        winners.Should().ContainSingle();
        winners[0].Prizes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldKeepEachPlayersPrizesToThemselves()
    {
        // Arrange
        GivenWinnings(Winning("user-1"), Winning("user-2"), Winning("user-1"));

        // Act
        var winners = await HandleAsync();

        // Assert
        winners.Single(winner => winner.UserId == "user-1").Prizes.Should().HaveCount(2);
        winners.Single(winner => winner.UserId == "user-2").Prizes.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldCarryThePlayersContactDetails()
    {
        // Arrange
        GivenWinnings(Winning("user-1") with { Email = "ada@example.test", FirstName = "Ada" });

        // Act
        var winner = (await HandleAsync()).Single();

        // Assert
        winner.Email.Should().Be("ada@example.test");
        winner.FirstName.Should().Be("Ada");
    }

    [Fact]
    public async Task Handle_ShouldReturnThePlayersInAStableOrder()
    {
        // Arrange - the read makes no promise about order.
        GivenWinnings(Winning("user-3"), Winning("user-1"), Winning("user-2"));

        // Act
        var winners = await HandleAsync();

        // Assert
        winners.Select(winner => winner.UserId).Should().Equal("user-1", "user-2", "user-3");
    }

    [Fact]
    public async Task Handle_ShouldListOnePlayersPrizesByLeagueName()
    {
        // Arrange - so the email reads the same way every time.
        GivenWinnings(
            Winning("user-1", leagueName: "Zulu League"),
            Winning("user-1", leagueName: "Alpha League"));

        // Act
        var winner = (await HandleAsync()).Single();

        // Assert
        winner.Prizes.Select(prize => prize.LeagueName).Should().Equal("Alpha League", "Zulu League");
    }

    #endregion

    #region What each prize says

    [Fact]
    public async Task Handle_ShouldCarryEveryPrizeDetail()
    {
        // Arrange
        GivenWinnings(Winning("user-1") with
        {
            LeagueId = 7,
            LeagueName = "The Office",
            LeaguePrizeSettingId = 11,
            PrizeType = PrizeType.Stages,
            PrizeDescription = "Quarter Finals Winner",
            Rank = 2,
            Stage = "Quarter Finals",
            Amount = 25m,
            RoundNumber = null,
            Month = null
        });

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.LeagueId.Should().Be(7);
        prize.LeagueName.Should().Be("The Office");
        prize.LeaguePrizeSettingId.Should().Be(11);
        prize.PrizeType.Should().Be(PrizeType.Stages);
        prize.PrizeDescription.Should().Be("Quarter Finals Winner");
        prize.Rank.Should().Be(2);
        prize.Stage.Should().Be("Quarter Finals");
        prize.Amount.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_ShouldNameTheRoundTheEmailIsAbout()
    {
        // Arrange
        GivenRound(Round(5, "Gameweek 5"));
        GivenWinnings(Winning("user-1"));

        // Act
        var winner = (await HandleAsync()).Single();

        // Assert
        winner.RoundName.Should().Be("Gameweek 5");
    }

    [Fact]
    public async Task Handle_ShouldNameTheRoundByItsNumber_WhenNobodyHasNamedIt()
    {
        // Arrange - the same rule every other screen applies. Two email reads used to put the raw column into a merge field.
        GivenRound(Round(5, string.Empty));
        GivenWinnings(Winning("user-1"));

        // Act
        var winner = (await HandleAsync()).Single();

        // Assert
        winner.RoundName.Should().Be("Round 5");
    }

    [Fact]
    public async Task Handle_ShouldNameTheRoundARoundPrizeWasWonIn()
    {
        // Arrange - a prize won in round 3 is named after round 3, not after the round being processed.
        GivenRound(Round(5, "Gameweek 5"));
        GivenWinnings(Winning("user-1") with { RoundNumber = 3 });
        GivenSeasonRounds(new SeasonRoundNameRow(3, "Gameweek 3"), new SeasonRoundNameRow(5, "Gameweek 5"));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.PrizeRoundName.Should().Be("Gameweek 3");
    }

    [Fact]
    public async Task Handle_ShouldNameARoundPrizesRoundByItsNumber_WhenNobodyHasNamedIt()
    {
        // Arrange
        GivenWinnings(Winning("user-1") with { RoundNumber = 3 });
        GivenSeasonRounds(new SeasonRoundNameRow(3, string.Empty));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.PrizeRoundName.Should().Be("Round 3");
    }

    [Fact]
    public async Task Handle_ShouldNotNameARoundForAPrizeThatIsNotAboutOne()
    {
        // Arrange - an overall or monthly prize spans the season, so there is no round to name.
        GivenWinnings(Winning("user-1") with { RoundNumber = null, Month = 3 });
        GivenSeasonRounds(new SeasonRoundNameRow(3, "Gameweek 3"));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.PrizeRoundName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNotNameARound_WhenTheSeasonHasNoSuchRound()
    {
        // Arrange - the round number on a winning is a number rather than a reference, so it can outlive the round.
        GivenWinnings(Winning("user-1") with { RoundNumber = 3 });
        GivenSeasonRounds(new SeasonRoundNameRow(5, "Gameweek 5"));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.PrizeRoundName.Should().BeNull();
    }

    #endregion

    #region Whether it has already been emailed about

    [Fact]
    public async Task Handle_ShouldReportAPrizeAsAlreadyEmailedAbout()
    {
        // Arrange
        GivenWinnings(Winning("user-1") with { LeaguePrizeSettingId = 11, RoundNumber = 3, Month = null });
        GivenNotifications(new PrizeNotificationRow("user-1", 11, 3, null));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.AlreadyNotified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldTreatASeasonLongPrizeWithNoRoundAndNoMonthAsTheSameOne()
    {
        // Both sides have no round and no month, which is what the ISNULL sentinel existed to make equal.
        GivenWinnings(Winning("user-1") with { LeaguePrizeSettingId = 11, RoundNumber = null, Month = null });
        GivenNotifications(new PrizeNotificationRow("user-1", 11, null, null));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.AlreadyNotified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotTreatARoundPrizeAsEmailedAbout_WhenAnEarlierRoundsWasTheOneSent()
    {
        // Arrange - the same slot pays out once a round, so the round has to be part of the match.
        GivenWinnings(Winning("user-1") with { LeaguePrizeSettingId = 11, RoundNumber = 5 });
        GivenNotifications(new PrizeNotificationRow("user-1", 11, 3, null));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.AlreadyNotified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotTreatAMonthlyPrizeAsEmailedAbout_WhenAnotherMonthsWasTheOneSent()
    {
        // Arrange - and the same for the month, which was the second half of the sentinel comparison.
        GivenWinnings(Winning("user-1") with { LeaguePrizeSettingId = 11, RoundNumber = null, Month = 4 });
        GivenNotifications(new PrizeNotificationRow("user-1", 11, null, 3));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.AlreadyNotified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotTreatAPrizeAsEmailedAbout_WhenTheEmailWasAboutAnotherPrize()
    {
        // Arrange
        GivenWinnings(Winning("user-1") with { LeaguePrizeSettingId = 11, RoundNumber = null, Month = null });
        GivenNotifications(new PrizeNotificationRow("user-1", 12, null, null));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.AlreadyNotified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotTreatAPrizeAsEmailedAbout_WhenSomebodyElseWasTheOneTold()
    {
        // Arrange - two people can win the same slot in the same round, one in each of two leagues sharing a season.
        GivenWinnings(Winning("user-1") with { LeaguePrizeSettingId = 11, RoundNumber = 3 });
        GivenNotifications(new PrizeNotificationRow("user-2", 11, 3, null));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.AlreadyNotified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportAPrizeAsNotEmailedAbout_WhenNothingHasBeenSent()
    {
        // Arrange
        GivenWinnings(Winning("user-1"));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.AlreadyNotified.Should().BeFalse();
    }

    #endregion

    private static RoundHeaderRow Round(int roundNumber, string displayName) =>
        new(RoundId, roundNumber, displayName, DeadlineUtc, SeasonId: 7, "2026/27", NumberOfRounds: 38,
            CompetitionType.League);

    private static PrizeWinningRow Winning(string userId, decimal amount = 100m, string leagueName = "The Office") =>
        new(userId, $"{userId}@example.com", "Ada", LeagueId: 7, leagueName, LeaguePrizeSettingId: 11,
            PrizeType.Overall, "1st Place", Rank: 1, Stage: null, amount, RoundNumber: null, Month: null);

    private void GivenRound(RoundHeaderRow round) => _round = round;

    private void GivenWinnings(params PrizeWinningRow[] winnings) => _winnings = [.. winnings];

    private void GivenNotifications(params PrizeNotificationRow[] notifications) => _notifications = [.. notifications];

    private void GivenSeasonRounds(params SeasonRoundNameRow[] rounds) => _seasonRounds = [.. rounds];

    private Task<IReadOnlyList<PrizeWinner>> HandleAsync()
    {
        _roundHeaderQuery.ExecuteAsync(RoundId, Arg.Any<CancellationToken>()).Returns(_round);
        _prizeWinnersQuery.ExecuteAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns(new PrizeWinnersData(_winnings, _notifications, _seasonRounds));

        return _handler.Handle(new GetPrizeWinnersForRoundQuery(RoundId), CancellationToken.None);
    }
}
