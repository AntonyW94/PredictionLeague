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
/// One player's season recap.
///
/// The position trajectory is what most of these tests are about: it recomputes the whole league's standings round
/// by round so the player can be told the highest position they ever held, which no other query does.
/// </summary>
public class GetSeasonRecapQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string MeId = "user-me";
    private const string RivalId = "user-rival";

    private static readonly DateTime January = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime February = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonRecapQuery _recapQuery = Substitute.For<ISeasonRecapQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetSeasonRecapQueryHandler _handler;

    public GetSeasonRecapQueryHandlerTests()
    {
        _handler = new GetSeasonRecapQueryHandler(_recapQuery, _membershipService);
    }

    #region Membership and a league that is not there

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, MeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _recapQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _recapQuery.ExecuteAsync(LeagueId, MeId, Arg.Any<CancellationToken>()).Returns((SeasonRecapData?)null);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyRecap_ForASeasonThatHasNotStarted()
    {
        // Arrange
        Given(members: [Me()]);

        // Act
        var recap = await HandleAsync();

        // Assert - the eleven ISNULL defaults, and no round numbers invented.
        recap.AveragePointsPerRound.Should().Be(0);
        recap.BestRoundPoints.Should().Be(0);
        recap.BestRoundNumber.Should().BeNull();
        recap.WorstRoundNumber.Should().BeNull();
        recap.TotalExactScores.Should().Be(0);
        recap.RoundsWon.Should().Be(0);
        recap.MonthsWon.Should().Be(0);
        recap.HighestPosition.Should().Be(0);
        recap.RoundsAtHighestPosition.Should().Be(0);
    }

    #endregion

    #region Money

    [Fact]
    public async Task Handle_ShouldTotalTheWinningsAndSubtractTheEntryFee()
    {
        // Arrange
        Given(leaguePrice: 20m, members: [Me()], winningAmounts: [30m, 45m]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.TotalWinnings.Should().Be(75m);
        recap.ProfitLoss.Should().Be(55m);
    }

    [Fact]
    public async Task Handle_ShouldReportALossWhenNothingWasWon()
    {
        // Arrange
        Given(leaguePrice: 20m, members: [Me()]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.TotalWinnings.Should().Be(0);
        recap.ProfitLoss.Should().Be(-20m);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheLeaguesPriceAndWhetherItIsFree()
    {
        // Arrange
        Given(isFree: true, leaguePrice: 0m, members: [Me()]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.IsFree.Should().BeTrue();
        recap.LeaguePrice.Should().Be(0m);
    }

    #endregion

    #region Final position

    [Fact]
    public async Task Handle_ShouldReportTheFinalPositionOnTotalPoints()
    {
        // Arrange
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1)],
            scores: [Score(MeId, 1, 10), Score(RivalId, 1, 30)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.FinalPosition.Should().Be(2);
        recap.TotalMembers.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldShareTheFinalPosition_WhenTotalsTie()
    {
        // Arrange
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1)],
            scores: [Score(MeId, 1, 30), Score(RivalId, 1, 30)]);

        // Act
        var recap = await HandleAsync();

        // Assert - joint first, as RANK() gave.
        recap.FinalPosition.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReportNoFinalPosition_WhenThePlayerIsNotAMember()
    {
        // Arrange - the membership check is mocked out, so this is the belt to its braces.
        Given(members: [Rival()], rounds: [Round(1)], scores: [Score(RivalId, 1, 30)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.FinalPosition.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyApprovedMembersScores()
    {
        // Arrange - a score exists for someone outside the league.
        Given(
            members: [Me()],
            rounds: [Round(1)],
            scores: [Score(MeId, 1, 10), Score("outsider", 1, 500)]);

        // Act
        var recap = await HandleAsync();

        // Assert - as everywhere else on the site.
        recap.FinalPosition.Should().Be(1);
        recap.TotalMembers.Should().Be(1);
    }

    #endregion

    #region Best, worst and average

    [Fact]
    public async Task Handle_ShouldReportTheBestAndWorstRounds()
    {
        // Arrange
        Given(
            members: [Me()],
            rounds: [Round(1), Round(2), Round(3)],
            scores: [Score(MeId, 1, 12), Score(MeId, 2, 31), Score(MeId, 3, 4)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.BestRoundPoints.Should().Be(31);
        recap.BestRoundNumber.Should().Be(2);
        recap.WorstRoundPoints.Should().Be(4);
        recap.WorstRoundNumber.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldPreferTheEarlierRound_WhenTheBestScoreIsMatched()
    {
        // Arrange
        Given(
            members: [Me()],
            rounds: [Round(1), Round(2)],
            scores: [Score(MeId, 1, 31), Score(MeId, 2, 31)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.BestRoundNumber.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreOtherPlayersRounds()
    {
        // Arrange
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2)],
            scores: [Score(MeId, 1, 12), Score(RivalId, 2, 99)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.BestRoundPoints.Should().Be(12);
        recap.BestRoundNumber.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldAverageOverTheRoundsPlayed_NotTheSeason()
    {
        // Arrange - three rounds exist, two were scored.
        Given(
            members: [Me()],
            rounds: [Round(1), Round(2), Round(3)],
            scores: [Score(MeId, 1, 10), Score(MeId, 2, 20)]);

        // Act
        var recap = await HandleAsync();

        // Assert - missing a round does not drag the average down.
        recap.AveragePointsPerRound.Should().Be(15m);
    }

    [Fact]
    public async Task Handle_ShouldIncludeARoundThatIsStillInProgress_InTheBestAndWorst()
    {
        // Arrange - no status filter on this block, unlike the wins counts.
        Given(
            members: [Me()],
            rounds: [Round(1), Round(2, status: RoundStatus.InProgress)],
            scores: [Score(MeId, 1, 12), Score(MeId, 2, 40)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.BestRoundNumber.Should().Be(2);
        recap.AveragePointsPerRound.Should().Be(26m);
    }

    [Fact]
    public async Task Handle_ShouldTotalTheExactScores()
    {
        // Arrange
        Given(members: [Me()], exactScoreCounts: [2, 0, 3]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.TotalExactScores.Should().Be(5);
    }

    #endregion

    #region Rounds and months won

    [Fact]
    public async Task Handle_ShouldCountTheRoundsThePlayerWon()
    {
        // Arrange - I win round 1, my rival wins round 2.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2)],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 2, 5), Score(RivalId, 2, 30)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.RoundsWon.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountASharedRoundWin()
    {
        // Arrange
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1)],
            scores: [Score(MeId, 1, 20), Score(RivalId, 1, 20)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.RoundsWon.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotCountARoundThatIsNotComplete()
    {
        // Arrange
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1, status: RoundStatus.InProgress)],
            scores: [Score(MeId, 1, 20), Score(RivalId, 1, 10)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.RoundsWon.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCountTheMonthsThePlayerWonOnTheMonthsTotal()
    {
        // Arrange - my rival takes two of January's three rounds, but one big week gives me the month.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1, January), Round(2, January), Round(3, January)],
            scores:
            [
                Score(MeId, 1, 1), Score(RivalId, 1, 10),
                Score(MeId, 2, 1), Score(RivalId, 2, 10),
                Score(MeId, 3, 30), Score(RivalId, 3, 1)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert - a month goes to the best total, not to whoever won most of its rounds.
        recap.MonthsWon.Should().Be(1);
        recap.RoundsWon.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSeparateMonthsByTheRoundsStartDate()
    {
        // Arrange - I take January, my rival takes February.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1, January), Round(2, February)],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 2, 5), Score(RivalId, 2, 30)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.MonthsWon.Should().Be(1);
    }

    #endregion

    #region The highest position ever held

    [Fact]
    public async Task Handle_ShouldReportTheHighestPositionEverHeld_NotTheFinalOne()
    {
        // Arrange - I lead after round 1 and am overtaken in round 2.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2)],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 2, 1), Score(RivalId, 2, 50)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert - this is the whole point of the recap: finished second, was first once.
        recap.FinalPosition.Should().Be(2);
        recap.HighestPosition.Should().Be(1);
        recap.RoundsAtHighestPosition.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRankOnTheRunningTotal_NotTheRoundsPoints()
    {
        // Arrange - my rival wins round 2 by a point but I stay ahead overall.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2)],
            scores:
            [
                Score(MeId, 1, 30), Score(RivalId, 1, 0),
                Score(MeId, 2, 10), Score(RivalId, 2, 11)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.HighestPosition.Should().Be(1);
        recap.RoundsAtHighestPosition.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldCountEveryRoundSpentAtTheHighestPosition()
    {
        // Arrange - first for three rounds running.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2), Round(3)],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 2, 20), Score(RivalId, 2, 10),
                Score(MeId, 3, 20), Score(RivalId, 3, 10)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.HighestPosition.Should().Be(1);
        recap.RoundsAtHighestPosition.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldStepThroughARoundNobodyScoredIn()
    {
        // Arrange - round 2 is completed but has no results at all.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2), Round(3)],
            scores:
            [
                Score(MeId, 1, 20), Score(RivalId, 1, 10),
                Score(MeId, 3, 20), Score(RivalId, 3, 10)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert - three steps, first at every one of them.
        recap.RoundsAtHighestPosition.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreRoundsBeforeThePlayerHadScored_WhenFindingTheHighestPosition()
    {
        // Arrange - nobody scores in round 1, so everyone is joint first on nothing.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2)],
            scores:
            [
                Score(MeId, 1, 0), Score(RivalId, 1, 0),
                Score(MeId, 2, 5), Score(RivalId, 2, 50)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert - being level on nothing is not a position worth claiming.
        recap.HighestPosition.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldCountRoundsAtTheHighestPosition_IncludingAnyBeforeThePlayerScored()
    {
        // Arrange - nobody scores in round 1 (joint first on nothing), then I lead outright in round 2.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2)],
            scores:
            [
                Score(MeId, 1, 0), Score(RivalId, 1, 0),
                Score(MeId, 2, 50), Score(RivalId, 2, 5)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert - the old statement applied its "had scored" guard when finding the best position but not when
        // counting how long it was held, so the scoreless round counts here. Pinned deliberately: this preserves
        // today's numbers, and is flagged in the plan document as worth a decision.
        recap.HighestPosition.Should().Be(1);
        recap.RoundsAtHighestPosition.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldReportNoHighestPosition_WhenThePlayerNeverScored()
    {
        // Arrange
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1)],
            scores: [Score(MeId, 1, 0), Score(RivalId, 1, 30)]);

        // Act
        var recap = await HandleAsync();

        // Assert
        recap.HighestPosition.Should().Be(0);
        recap.RoundsAtHighestPosition.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreRoundsThatAreNotComplete_InTheTrajectory()
    {
        // Arrange - I am ahead only while round 2 is still in play.
        Given(
            members: [Me(), Rival()],
            rounds: [Round(1), Round(2, status: RoundStatus.InProgress)],
            scores:
            [
                Score(MeId, 1, 10), Score(RivalId, 1, 30),
                Score(MeId, 2, 100), Score(RivalId, 2, 0)
            ]);

        // Act
        var recap = await HandleAsync();

        // Assert - a position held mid-round is not a position held.
        recap.HighestPosition.Should().Be(2);
        recap.RoundsAtHighestPosition.Should().Be(1);
    }

    #endregion

    private void Given(
        bool isFree = false,
        decimal leaguePrice = 10m,
        IReadOnlyList<LeaderboardParticipantRow>? members = null,
        IReadOnlyList<SeasonRecapRoundRow>? rounds = null,
        IReadOnlyList<MemberRoundPointsByRoundRow>? scores = null,
        IReadOnlyList<int>? exactScoreCounts = null,
        IReadOnlyList<decimal>? winningAmounts = null)
    {
        _recapQuery
            .ExecuteAsync(LeagueId, MeId, Arg.Any<CancellationToken>())
            .Returns(new SeasonRecapData(
                isFree,
                leaguePrice,
                members ?? [],
                rounds ?? [],
                scores ?? [],
                exactScoreCounts ?? [],
                winningAmounts ?? []));
    }

    private async Task<SeasonRecapDto> HandleAsync() =>
        await _handler.Handle(new GetSeasonRecapQuery(LeagueId, MeId), CancellationToken.None);

    private static LeaderboardParticipantRow Me() => new(MeId, "Ada", "Lovelace");

    private static LeaderboardParticipantRow Rival() => new(RivalId, "Grace", "Hopper");

    private static SeasonRecapRoundRow Round(
        int roundNumber,
        DateTime? startDateUtc = null,
        RoundStatus status = RoundStatus.Completed) =>
        new(roundNumber, roundNumber, startDateUtc ?? January, status);

    private static MemberRoundPointsByRoundRow Score(string userId, int roundId, int points) =>
        new(userId, roundId, points);
}
