using FluentAssertions;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeagueWinnersAndRankingsTests
{
    private static readonly DateTime FixedDate = new(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    private static League CreateLeagueWithMembers(
        params (string UserId, (int RoundId, int BoostedPoints, int ExactScoreCount)[] Results)[] members)
    {
        var leagueMembers = members.Select(m =>
            new LeagueMember(
                leagueId: 1,
                userId: m.UserId,
                status: LeagueMemberStatus.Approved,
                isAlertDismissed: false,
                joinedAtUtc: FixedDate,
                approvedAtUtc: FixedDate,
                roundResults: m.Results.Select(r => new LeagueRoundResult(
                    leagueId: 1,
                    roundId: r.RoundId,
                    userId: m.UserId,
                    basePoints: r.BoostedPoints,
                    boostedPoints: r.BoostedPoints,
                    hasBoost: false,
                    appliedBoostCode: null,
                    exactScoreCount: r.ExactScoreCount
                )).ToList()
            )).ToList();

        return new League(
            id: 1, name: "Test League", seasonId: 1,
            administratorUserId: "admin", entryCode: "ABC123",
            createdAtUtc: FixedDate,
            entryDeadlineUtc: FixedDate.AddDays(30),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: leagueMembers, prizeSettings: null);
    }

    private static League CreateEmptyLeague()
    {
        return new League(
            id: 1, name: "Test League", seasonId: 1,
            administratorUserId: "admin", entryCode: "ABC123",
            createdAtUtc: FixedDate,
            entryDeadlineUtc: FixedDate.AddDays(30),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: null, prizeSettings: null);
    }

    #region GetRoundWinners

    [Fact]
    public void GetRoundWinners_ShouldReturnEmptyList_WhenNoMembers()
    {
        // Arrange
        var league = CreateEmptyLeague();

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnEmptyList_WhenAllScoresAreZero()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 0, 0)]),
            ("user-b", [(1, 0, 0)]));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnSingleWinner_WhenOnePlayerHasHighestScore()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0)]),
            ("user-b", [(1, 5, 0)]));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-a");
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnMultipleWinners_WhenPlayersAreTied()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0)]),
            ("user-b", [(1, 10, 0)]));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().HaveCount(2);
        winners.Select(w => w.UserId).Should().Contain(["user-a", "user-b"]);
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnAllTiedMembers_WhenThreePlayersAreTied()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0)]),
            ("user-b", [(1, 10, 0)]),
            ("user-c", [(1, 10, 0)]));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().HaveCount(3);
        winners.Select(w => w.UserId).Should().Contain(["user-a", "user-b", "user-c"]);
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnEmptyList_WhenNoResultsForRound()
    {
        // Arrange — members have results for round 2, but we query round 1
        var league = CreateLeagueWithMembers(
            ("user-a", [(2, 10, 0)]),
            ("user-b", [(2, 5, 0)]));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnEmptyList_WhenMembersHaveNoRoundResults()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", []),
            ("user-b", []));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetRoundWinners_ShouldUseBoostPoints_NotBasePoints()
    {
        // Arrange — user-a has lower base but higher boosted, user-b has higher base but lower boosted
        var memberA = new LeagueMember(
            leagueId: 1, userId: "user-a",
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false, joinedAtUtc: FixedDate, approvedAtUtc: FixedDate,
            roundResults: [new LeagueRoundResult(
                leagueId: 1, roundId: 1, userId: "user-a",
                basePoints: 5, boostedPoints: 10,
                hasBoost: true, appliedBoostCode: "DoubleUp", exactScoreCount: 0)]);

        var memberB = new LeagueMember(
            leagueId: 1, userId: "user-b",
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false, joinedAtUtc: FixedDate, approvedAtUtc: FixedDate,
            roundResults: [new LeagueRoundResult(
                leagueId: 1, roundId: 1, userId: "user-b",
                basePoints: 8, boostedPoints: 8,
                hasBoost: false, appliedBoostCode: null, exactScoreCount: 0)]);

        var league = new League(
            id: 1, name: "Test League", seasonId: 1,
            administratorUserId: "admin", entryCode: "ABC123",
            createdAtUtc: FixedDate,
            entryDeadlineUtc: FixedDate.AddDays(30),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: [memberA, memberB], prizeSettings: null);

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-a");
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnSingleMember_WhenOnlyOneMemberWithPoints()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 5, 0)]));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-a");
    }

    [Fact]
    public void GetRoundWinners_ShouldReturnEmptyList_WhenOnlyOneMemberWithZeroPoints()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 0, 0)]));

        // Act
        var winners = league.GetRoundWinners(roundId: 1);

        // Assert
        winners.Should().BeEmpty();
    }

    #endregion

    #region GetPeriodWinners

    [Fact]
    public void GetPeriodWinners_ShouldReturnEmptyList_WhenNoMembers()
    {
        // Arrange
        var league = CreateEmptyLeague();

        // Act
        var winners = league.GetPeriodWinners([1, 2]);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetPeriodWinners_ShouldSumPointsAcrossRounds_WhenMultipleRoundsInPeriod()
    {
        // Arrange — User-A: 5+5=10, User-B: 3+8=11
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 5, 0), (2, 5, 0)]),
            ("user-b", [(1, 3, 0), (2, 8, 0)]));

        // Act
        var winners = league.GetPeriodWinners([1, 2]);

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-b");
    }

    [Fact]
    public void GetPeriodWinners_ShouldReturnMultipleWinners_WhenTied()
    {
        // Arrange — both sum to 10
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 4, 0), (2, 6, 0)]),
            ("user-b", [(1, 7, 0), (2, 3, 0)]));

        // Act
        var winners = league.GetPeriodWinners([1, 2]);

        // Assert
        winners.Should().HaveCount(2);
        winners.Select(w => w.UserId).Should().Contain(["user-a", "user-b"]);
    }

    [Fact]
    public void GetPeriodWinners_ShouldIgnoreRoundsOutsidePeriod()
    {
        // Arrange — User-A scores 10 in round 3 (outside period), User-B scores 5 in round 1 (inside period)
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 2, 0), (3, 10, 0)]),
            ("user-b", [(1, 5, 0), (3, 1, 0)]));

        // Act — period only includes round 1
        var winners = league.GetPeriodWinners([1]);

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-b");
    }

    [Fact]
    public void GetPeriodWinners_ShouldReturnEmptyList_WhenAllTotalScoresAreZero()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 0, 0), (2, 0, 0)]),
            ("user-b", [(1, 0, 0), (2, 0, 0)]));

        // Act
        var winners = league.GetPeriodWinners([1, 2]);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetPeriodWinners_ShouldReturnEmptyList_WhenMembersHaveNoRoundResults()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", []),
            ("user-b", []));

        // Act
        var winners = league.GetPeriodWinners([1, 2]);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetPeriodWinners_ShouldReturnEmptyList_WhenEmptyRoundIdsList()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0)]),
            ("user-b", [(1, 5, 0)]));

        // Act
        var winners = league.GetPeriodWinners([]);

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetPeriodWinners_ShouldHandleMembersWithPartialResults()
    {
        // Arrange — User-A has results for rounds 1+2, User-B only round 1
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 3, 0), (2, 4, 0)]),
            ("user-b", [(1, 8, 0)]));

        // Act
        var winners = league.GetPeriodWinners([1, 2]);

        // Assert — User-B: 8, User-A: 3+4=7
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-b");
    }

    #endregion

    #region GetOverallRankings

    [Fact]
    public void GetOverallRankings_ShouldReturnEmptyList_WhenNoMembers()
    {
        // Arrange
        var league = CreateEmptyLeague();

        // Act
        var rankings = league.GetOverallRankings();

        // Assert
        rankings.Should().BeEmpty();
    }

    [Fact]
    public void GetOverallRankings_ShouldRankByTotalBoostedPoints()
    {
        // Arrange — A:20, B:15, C:10
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0), (2, 10, 0)]),
            ("user-b", [(1, 8, 0), (2, 7, 0)]),
            ("user-c", [(1, 5, 0), (2, 5, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert
        rankings.Should().HaveCount(3);
        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().ContainSingle(m => m.UserId == "user-a");
        rankings[1].Rank.Should().Be(2);
        rankings[1].Members.Should().ContainSingle(m => m.UserId == "user-b");
        rankings[2].Rank.Should().Be(3);
        rankings[2].Members.Should().ContainSingle(m => m.UserId == "user-c");
    }

    [Fact]
    public void GetOverallRankings_ShouldGroupTiedPlayers_WhenScoresAreEqual()
    {
        // Arrange — A:20, B:20, C:10
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0), (2, 10, 0)]),
            ("user-b", [(1, 12, 0), (2, 8, 0)]),
            ("user-c", [(1, 5, 0), (2, 5, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert
        rankings.Should().HaveCount(2);
        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().HaveCount(2);
        rankings[0].Members.Select(m => m.UserId).Should().Contain(["user-a", "user-b"]);
    }

    [Fact]
    public void GetOverallRankings_ShouldSkipRanks_WhenPlayersAreTied()
    {
        // Arrange — A:20, B:20, C:10
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0), (2, 10, 0)]),
            ("user-b", [(1, 12, 0), (2, 8, 0)]),
            ("user-c", [(1, 5, 0), (2, 5, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert — Rank 1 (tied), then Rank 3 (not 2)
        rankings[0].Rank.Should().Be(1);
        rankings[1].Rank.Should().Be(3);
    }

    [Fact]
    public void GetOverallRankings_ShouldHandleAllPlayersTied()
    {
        // Arrange — all have 10 points
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0)]),
            ("user-b", [(1, 10, 0)]),
            ("user-c", [(1, 10, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert
        rankings.Should().HaveCount(1);
        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().HaveCount(3);
    }

    [Fact]
    public void GetOverallRankings_ShouldSumAcrossAllRounds()
    {
        // Arrange — User-A: 3+4+5=12
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 3, 0), (2, 4, 0), (3, 5, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert — single member with total 12 at rank 1
        rankings.Should().HaveCount(1);
        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().ContainSingle(m => m.UserId == "user-a");
    }

    [Fact]
    public void GetOverallRankings_ShouldIncludeAllZeroScoreMembers()
    {
        // Arrange — all have 0 points (unlike GetRoundWinners which returns empty)
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 0, 0)]),
            ("user-b", [(1, 0, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert — includes all members at rank 1
        rankings.Should().HaveCount(1);
        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().HaveCount(2);
    }

    [Fact]
    public void GetOverallRankings_ShouldReturnSingleRanking_WhenOnlyOneMember()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert
        rankings.Should().HaveCount(1);
        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().ContainSingle(m => m.UserId == "user-a");
    }

    [Fact]
    public void GetOverallRankings_ShouldHandleMembersWithNoRoundResults()
    {
        // Arrange — members exist but have empty RoundResults (0 points each)
        var league = CreateLeagueWithMembers(
            ("user-a", []),
            ("user-b", []));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert — all at rank 1 with 0 points
        rankings.Should().HaveCount(1);
        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().HaveCount(2);
    }

    [Fact]
    public void GetOverallRankings_ShouldHandleMultipleTiedGroups()
    {
        // Arrange — A:30, B:30, C:20, D:20, E:10
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 15, 0), (2, 15, 0)]),
            ("user-b", [(1, 20, 0), (2, 10, 0)]),
            ("user-c", [(1, 10, 0), (2, 10, 0)]),
            ("user-d", [(1, 12, 0), (2, 8, 0)]),
            ("user-e", [(1, 5, 0), (2, 5, 0)]));

        // Act
        var rankings = league.GetOverallRankings();

        // Assert — Rank 1=[A,B], Rank 3=[C,D], Rank 5=[E]
        rankings.Should().HaveCount(3);

        rankings[0].Rank.Should().Be(1);
        rankings[0].Members.Should().HaveCount(2);
        rankings[0].Members.Select(m => m.UserId).Should().Contain(["user-a", "user-b"]);

        rankings[1].Rank.Should().Be(3);
        rankings[1].Members.Should().HaveCount(2);
        rankings[1].Members.Select(m => m.UserId).Should().Contain(["user-c", "user-d"]);

        rankings[2].Rank.Should().Be(5);
        rankings[2].Members.Should().HaveCount(1);
        rankings[2].Members[0].UserId.Should().Be("user-e");
    }

    #endregion

    #region GetMostExactScoresWinners

    [Fact]
    public void GetMostExactScoresWinners_ShouldReturnEmptyList_WhenNoMembers()
    {
        // Arrange
        var league = CreateEmptyLeague();

        // Act
        var winners = league.GetMostExactScoresWinners();

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetMostExactScoresWinners_ShouldReturnEmptyList_WhenNoExactScores()
    {
        // Arrange — all ExactScoreCount = 0
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 0)]),
            ("user-b", [(1, 5, 0)]));

        // Act
        var winners = league.GetMostExactScoresWinners();

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetMostExactScoresWinners_ShouldReturnSingleWinner_WhenOneHasMost()
    {
        // Arrange — A:5, B:3
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 5)]),
            ("user-b", [(1, 10, 3)]));

        // Act
        var winners = league.GetMostExactScoresWinners();

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-a");
    }

    [Fact]
    public void GetMostExactScoresWinners_ShouldReturnMultipleWinners_WhenTied()
    {
        // Arrange — A:5, B:5
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 5)]),
            ("user-b", [(1, 10, 5)]));

        // Act
        var winners = league.GetMostExactScoresWinners();

        // Assert
        winners.Should().HaveCount(2);
        winners.Select(w => w.UserId).Should().Contain(["user-a", "user-b"]);
    }

    [Fact]
    public void GetMostExactScoresWinners_ShouldSumExactScoresAcrossRounds()
    {
        // Arrange — A: 2+3=5, B: 4+0=4
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 2), (2, 10, 3)]),
            ("user-b", [(1, 10, 4), (2, 10, 0)]));

        // Act
        var winners = league.GetMostExactScoresWinners();

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-a");
    }

    [Fact]
    public void GetMostExactScoresWinners_ShouldReturnEmptyList_WhenMembersHaveNoRoundResults()
    {
        // Arrange
        var league = CreateLeagueWithMembers(
            ("user-a", []),
            ("user-b", []));

        // Act
        var winners = league.GetMostExactScoresWinners();

        // Assert
        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetMostExactScoresWinners_ShouldReturnSingleMember_WhenOnlyOneMemberHasExactScores()
    {
        // Arrange — A:3, B:0
        var league = CreateLeagueWithMembers(
            ("user-a", [(1, 10, 3)]),
            ("user-b", [(1, 10, 0)]));

        // Act
        var winners = league.GetMostExactScoresWinners();

        // Assert
        winners.Should().HaveCount(1);
        winners[0].UserId.Should().Be("user-a");
    }

    #endregion
}
