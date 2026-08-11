using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's records tile - ten records that were ten <c>OUTER APPLY</c> blocks.
///
/// Each region below is one record, and each was a rule nothing could test: which round counts as the worst, what
/// makes a round won, which of two joint holders gets named.
/// </summary>
public class GetLeagueRecordsQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string ViewerId = "user-me";

    private static readonly DateTime January = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime February = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueRecordsQuery _recordsQuery = Substitute.For<ILeagueRecordsQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetLeagueRecordsQueryHandler _handler;

    public GetLeagueRecordsQueryHandlerTests()
    {
        _handler = new GetLeagueRecordsQueryHandler(_recordsQuery, _membershipService);
    }

    #region Membership and a league that is not there

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _recordsQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _recordsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((LeagueRecordsData?)null);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReportEveryRecordAsEmpty_ForALeagueThatHasPlayedNothing()
    {
        // Arrange
        Given();

        // Act
        var records = await HandleAsync();

        // Assert - the ten ISNULL(..., 0) defaults, and no names invented.
        records.TopRoundPlayerName.Should().BeNull();
        records.TopRoundPoints.Should().Be(0);
        records.TopRoundNumber.Should().BeNull();
        records.LowestRoundPlayerName.Should().BeNull();
        records.MostExactInRoundCount.Should().Be(0);
        records.ChampionName.Should().BeNull();
        records.TopEarnerAmount.Should().Be(0);
        records.MostRoundsWonCount.Should().Be(0);
        records.MostMonthsWonCount.Should().Be(0);
        records.TotalExactScores.Should().Be(0);
        records.BiggestPrizeDescription.Should().BeNull();
        records.HighestGameweekRoundNumber.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCarryWhetherTheLeagueIsFree()
    {
        // Arrange
        Given(isFree: true);

        // Act
        var records = await HandleAsync();

        // Assert
        records.IsFree.Should().BeTrue();
    }

    #endregion

    #region Highest and lowest round

    [Fact]
    public async Task Handle_ShouldReportTheHighestSingleRound()
    {
        // Arrange
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 12),
            Score("u2", "Grace", "Hopper", round: 2, points: 31)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.TopRoundPlayerName.Should().Be("Grace H");
        records.TopRoundPoints.Should().Be(31);
        records.TopRoundNumber.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldGiveTheHighestRoundToTheEarlierRound_WhenTwoScoresMatch()
    {
        // Arrange
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 5, points: 31),
            Score("u2", "Grace", "Hopper", round: 2, points: 31)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - whoever got there first holds it.
        records.TopRoundNumber.Should().Be(2);
        records.TopRoundPlayerName.Should().Be("Grace H");
    }

    [Fact]
    public async Task Handle_ShouldReportTheLowestRound()
    {
        // Arrange
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 12),
            Score("u2", "Grace", "Hopper", round: 2, points: 3)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.LowestRoundPlayerName.Should().Be("Grace H");
        records.LowestRoundPoints.Should().Be(3);
        records.LowestRoundNumber.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotCountARoundNobodyEntered_TowardsTheLowestRound()
    {
        // Arrange - Grace scored nothing in round 2 because she never predicted it.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 12),
            Score("u2", "Grace", "Hopper", round: 2, points: 0, hasAnyPrediction: false)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - the record is about football, not about who joined late.
        records.LowestRoundPlayerName.Should().Be("Ada L");
        records.LowestRoundPoints.Should().Be(12);
    }

    [Fact]
    public async Task Handle_ShouldReportNoLowestRound_WhenNobodyHasEnteredAnything()
    {
        // Arrange
        Given(roundScores: [Score("u1", "Ada", "Lovelace", round: 1, points: 0, hasAnyPrediction: false)]);

        // Act
        var records = await HandleAsync();

        // Assert - but the highest round still stands, since it never had the predicted test.
        records.LowestRoundPlayerName.Should().BeNull();
        records.LowestRoundNumber.Should().BeNull();
        records.TopRoundPlayerName.Should().Be("Ada L");
    }

    #endregion

    #region Champion

    [Fact]
    public async Task Handle_ShouldReportTheChampionOnTotalPoints()
    {
        // Arrange
        Given(
            members: [Member("u1", "Ada", "Lovelace"), Member("u2", "Grace", "Hopper")],
            roundScores:
            [
                Score("u1", "Ada", "Lovelace", round: 1, points: 10),
                Score("u1", "Ada", "Lovelace", round: 2, points: 10),
                Score("u2", "Grace", "Hopper", round: 1, points: 15)
            ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.ChampionName.Should().Be("Ada L");
        records.ChampionPoints.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ShouldStillConsiderAMemberWhoHasNeverScored()
    {
        // Arrange - one member, no results at all.
        Given(members: [Member("u1", "Ada", "Lovelace")]);

        // Act
        var records = await HandleAsync();

        // Assert - the old block's LEFT JOIN and ISNULL(SUM(...), 0).
        records.ChampionName.Should().Be("Ada L");
        records.ChampionPoints.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNameTheChampionAlphabetically_WhenTotalsTie()
    {
        // Arrange - the old block ordered by points alone, so this was the plan's choice.
        Given(
            members: [Member("u2", "Grace", "Hopper"), Member("u1", "Ada", "Lovelace")],
            roundScores:
            [
                Score("u1", "Ada", "Lovelace", round: 1, points: 10),
                Score("u2", "Grace", "Hopper", round: 1, points: 10)
            ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.ChampionName.Should().Be("Ada L");
    }

    #endregion

    #region Exact scores

    [Fact]
    public async Task Handle_ShouldReportTheMostExactScoresInARound()
    {
        // Arrange
        Given(exactScores:
        [
            Exact("u1", "Ada", "Lovelace", round: 1, count: 2),
            Exact("u2", "Grace", "Hopper", round: 3, count: 5)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.MostExactInRoundPlayerName.Should().Be("Grace H");
        records.MostExactInRoundCount.Should().Be(5);
        records.MostExactInRoundNumber.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldTotalEveryExactScoreInTheLeague()
    {
        // Arrange
        Given(exactScores:
        [
            Exact("u1", "Ada", "Lovelace", round: 1, count: 2),
            Exact("u2", "Grace", "Hopper", round: 1, count: 5),
            Exact("u1", "Ada", "Lovelace", round: 2, count: 1)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.TotalExactScores.Should().Be(8);
    }

    #endregion

    #region Rounds won

    [Fact]
    public async Task Handle_ShouldCountRoundsWon()
    {
        // Arrange - Ada wins rounds 1 and 2, Grace wins round 3.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 20, roundId: 1),
            Score("u2", "Grace", "Hopper", round: 1, points: 10, roundId: 1),
            Score("u1", "Ada", "Lovelace", round: 2, points: 20, roundId: 2),
            Score("u2", "Grace", "Hopper", round: 2, points: 10, roundId: 2),
            Score("u1", "Ada", "Lovelace", round: 3, points: 5, roundId: 3),
            Score("u2", "Grace", "Hopper", round: 3, points: 30, roundId: 3)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.MostRoundsWonPlayerName.Should().Be("Ada L");
        records.MostRoundsWonCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldCountASharedRoundWinForBothPlayers()
    {
        // Arrange - drawn round 1, then Grace wins round 2 outright.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 20, roundId: 1),
            Score("u2", "Grace", "Hopper", round: 1, points: 20, roundId: 1),
            Score("u2", "Grace", "Hopper", round: 2, points: 30, roundId: 2)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - RANK rather than ROW_NUMBER: a shared win counts for both, so Grace has two.
        records.MostRoundsWonPlayerName.Should().Be("Grace H");
        records.MostRoundsWonCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotCountARoundNobodyScoredIn()
    {
        // Arrange - a completed round where everyone scored nothing.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 0, roundId: 1),
            Score("u2", "Grace", "Hopper", round: 1, points: 0, roundId: 1)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - otherwise a round created but not yet scored hands everyone a win.
        records.MostRoundsWonPlayerName.Should().BeNull();
        records.MostRoundsWonCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotCountARoundThatIsNotComplete()
    {
        // Arrange
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 20, roundId: 1, status: RoundStatus.InProgress),
            Score("u2", "Grace", "Hopper", round: 1, points: 10, roundId: 1, status: RoundStatus.InProgress)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - a round in play has not been won yet.
        records.MostRoundsWonCount.Should().Be(0);
    }

    #endregion

    #region Months won

    [Fact]
    public async Task Handle_ShouldCountMonthsWonOnTheMonthsTotal()
    {
        // Arrange - Grace wins each single round in January, but Ada's two rounds total more.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 14, roundId: 1, startDateUtc: January),
            Score("u2", "Grace", "Hopper", round: 1, points: 15, roundId: 1, startDateUtc: January),
            Score("u1", "Ada", "Lovelace", round: 2, points: 14, roundId: 2, startDateUtc: January),
            Score("u2", "Grace", "Hopper", round: 2, points: 12, roundId: 2, startDateUtc: January)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - a month is not just its best round.
        records.MostMonthsWonPlayerName.Should().Be("Ada L");
        records.MostMonthsWonCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSeparateMonthsByTheRoundsStartDate()
    {
        // Arrange - Ada takes January, Grace takes February.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 20, roundId: 1, startDateUtc: January),
            Score("u2", "Grace", "Hopper", round: 1, points: 10, roundId: 1, startDateUtc: January),
            Score("u1", "Ada", "Lovelace", round: 2, points: 5, roundId: 2, startDateUtc: February),
            Score("u2", "Grace", "Hopper", round: 2, points: 30, roundId: 2, startDateUtc: February)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - one each, so the alphabetical tie-break names Ada.
        records.MostMonthsWonCount.Should().Be(1);
        records.MostMonthsWonPlayerName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldTreatTheSameMonthInDifferentYearsAsTwoMonths()
    {
        // Arrange - January 2026 and January 2027.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 20, roundId: 1, startDateUtc: January),
            Score("u1", "Ada", "Lovelace", round: 2, points: 20, roundId: 2, startDateUtc: January.AddYears(1))
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - the old block partitioned by MONTH and YEAR, not MONTH alone.
        records.MostMonthsWonCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAMonthNobodyScoredIn()
    {
        // Arrange
        Given(roundScores: [Score("u1", "Ada", "Lovelace", round: 1, points: 0, roundId: 1, startDateUtc: January)]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.MostMonthsWonCount.Should().Be(0);
    }

    #endregion

    #region Winnings

    [Fact]
    public async Task Handle_ShouldReportTheTopEarnerOnTotalWinnings()
    {
        // Arrange - Grace won one bigger prize, Ada two smaller ones worth more together.
        Given(winnings:
        [
            Winning("u1", "Ada", "Lovelace", 30m),
            Winning("u1", "Ada", "Lovelace", 30m),
            Winning("u2", "Grace", "Hopper", 50m)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.TopEarnerName.Should().Be("Ada L");
        records.TopEarnerAmount.Should().Be(60m);
    }

    [Fact]
    public async Task Handle_ShouldReportTheBiggestSinglePrize()
    {
        // Arrange
        Given(winnings:
        [
            Winning("u1", "Ada", "Lovelace", 30m),
            Winning("u1", "Ada", "Lovelace", 30m),
            Winning("u2", "Grace", "Hopper", 50m)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - the top earner and the biggest prize are different questions.
        records.BiggestPrizePlayerName.Should().Be("Grace H");
        records.BiggestPrizeAmount.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_ShouldGiveTheBiggestPrizeToWhicheverWasAwardedFirst_WhenAmountsTie()
    {
        // Arrange
        Given(winnings:
        [
            Winning("u1", "Ada", "Lovelace", 50m, awardedDateUtc: February),
            Winning("u2", "Grace", "Hopper", 50m, awardedDateUtc: January)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.BiggestPrizePlayerName.Should().Be("Grace H");
    }

    [Fact]
    public async Task Handle_ShouldLabelABiggestPrizeThatHasItsOwnWording()
    {
        // Arrange
        Given(winnings: [Winning("u1", "Ada", "Lovelace", 50m, prizeDescription: "1st Place")]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.BiggestPrizeDescription.Should().Be("1st Place");
    }

    [Fact]
    public async Task Handle_ShouldLabelABiggestPrizeByItsRound()
    {
        // Arrange
        Given(winnings:
        [
            Winning("u1", "Ada", "Lovelace", 50m, prizeType: PrizeType.Round, roundNumber: 12)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.BiggestPrizeDescription.Should().Be("Round 12");
    }

    [Fact]
    public async Task Handle_ShouldLabelABiggestPrizeByItsMonth()
    {
        // Arrange
        Given(winnings:
        [
            Winning("u1", "Ada", "Lovelace", 50m, prizeType: PrizeType.Monthly, month: 3)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert - and in English regardless of what language the database speaks.
        records.BiggestPrizeDescription.Should().Be("March");
    }

    #endregion

    #region Highest scoring round

    [Fact]
    public async Task Handle_ShouldReportTheRoundTheLeagueScoredMostIn()
    {
        // Arrange - round 2 has the single best individual score, round 1 the better combined total.
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 1, points: 20, roundId: 1),
            Score("u2", "Grace", "Hopper", round: 1, points: 20, roundId: 1),
            Score("u1", "Ada", "Lovelace", round: 2, points: 31, roundId: 2)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.HighestGameweekRoundNumber.Should().Be(1);
        records.HighestGameweekPoints.Should().Be(40);
    }

    [Fact]
    public async Task Handle_ShouldGiveTheHighestScoringRoundToTheEarlierRound_WhenTotalsTie()
    {
        // Arrange
        Given(roundScores:
        [
            Score("u1", "Ada", "Lovelace", round: 5, points: 20, roundId: 5),
            Score("u1", "Ada", "Lovelace", round: 2, points: 20, roundId: 2)
        ]);

        // Act
        var records = await HandleAsync();

        // Assert
        records.HighestGameweekRoundNumber.Should().Be(2);
    }

    #endregion

    private void Given(
        bool isFree = false,
        IReadOnlyList<LeaderboardParticipantRow>? members = null,
        IReadOnlyList<LeagueRecordRoundScoreRow>? roundScores = null,
        IReadOnlyList<LeagueRecordExactScoreRow>? exactScores = null,
        IReadOnlyList<LeagueRecordWinningRow>? winnings = null)
    {
        _recordsQuery
            .ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(new LeagueRecordsData(
                isFree, members ?? [], roundScores ?? [], exactScores ?? [], winnings ?? []));
    }

    private async Task<LeagueRecordsDto> HandleAsync() =>
        await _handler.Handle(new GetLeagueRecordsQuery(LeagueId, ViewerId), CancellationToken.None);

    private static LeaderboardParticipantRow Member(string userId, string firstName, string lastName) =>
        new(userId, firstName, lastName);

    private static LeagueRecordRoundScoreRow Score(
        string userId,
        string firstName,
        string lastName,
        int round,
        int points,
        int? roundId = null,
        DateTime? startDateUtc = null,
        RoundStatus status = RoundStatus.Completed,
        bool hasAnyPrediction = true) =>
        new(
            userId,
            firstName,
            lastName,
            roundId ?? round,
            round,
            startDateUtc ?? January,
            status,
            points,
            hasAnyPrediction);

    private static LeagueRecordExactScoreRow Exact(
        string userId, string firstName, string lastName, int round, int count) =>
        new(userId, firstName, lastName, round, count);

    private static LeagueRecordWinningRow Winning(
        string userId,
        string firstName,
        string lastName,
        decimal amount,
        DateTime? awardedDateUtc = null,
        PrizeType prizeType = PrizeType.Overall,
        string? prizeDescription = null,
        int? roundNumber = null,
        int? month = null) =>
        new(
            userId,
            firstName,
            lastName,
            amount,
            awardedDateUtc ?? January,
            prizeType,
            prizeDescription,
            roundNumber,
            month);
}
