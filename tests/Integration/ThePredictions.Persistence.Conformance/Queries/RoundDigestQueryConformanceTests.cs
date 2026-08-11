using FluentAssertions;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IRoundDigestQuery"/> implementation must return: four sets of facts about one round, with none of
/// the deciding done.
///
/// The statement this replaces joined six tables and two CTEs, and every rule the email needs was inside it. What these
/// tests pin is the opposite: that the rows arrive unfiltered, unranked and unordered, including the ones a rule will
/// later throw away.
/// </summary>
public abstract class RoundDigestQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IRoundDigestQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region The season's rounds

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothingAtAll_ForARoundThatDoesNotExist()
    {
        // Act
        var data = await Query.ExecuteAsync(-1, CancellationToken.None);

        // Assert - the caller finds the round it asked about among these, so an empty set is how it learns there is none.
        data.SeasonRounds.Should().BeEmpty();
        data.Players.Should().BeEmpty();
        data.Memberships.Should().BeEmpty();
        data.LeagueScores.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfTheSeasonIncludingTheOneAskedAbout()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        var nextRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 13, Deadline.AddDays(7));
        var earlierRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 11, Deadline.AddDays(-7));

        // Act
        var data = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - which one comes next is a rule, so the read hands over all of them rather than picking.
        data.SeasonRounds.Select(round => round.Id).Should().BeEquivalentTo([roundId, nextRoundId, earlierRoundId]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsNameAndDeadline()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline, displayName: "Quarter Finals");

        // Act
        var round = (await Query.ExecuteAsync(roundId, CancellationToken.None)).SeasonRounds.Single();

        // Assert
        round.DisplayName.Should().Be("Quarter Finals");
        round.RoundNumber.Should().Be(12);
        round.DeadlineUtc.Should().Be(Deadline);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnABlankNameExactlyAsStored()
    {
        // Arrange - no round in the database is unnamed today, but the column allows it and every other screen guards
        // against it. Naming such a round by its number is a rule, so the read must hand the blank over untouched.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline, displayName: string.Empty);

        // Act
        var round = (await Query.ExecuteAsync(roundId, CancellationToken.None)).SeasonRounds.Single();

        // Assert
        round.DisplayName.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnotherSeasonsRounds()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        await Seed.AddRoundAsync(otherSeasonId, 13, Deadline.AddYears(1));

        // Act
        var data = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        data.SeasonRounds.Select(round => round.Id).Should().Equal(roundId);
    }

    #endregion

    #region The players

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryPlayerScoredForTheRoundWithTheirOutcomes()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);

        await Seed.AddRoundResultAsync(roundId, backdrop.UserId, exactScoreCount: 3, correctResultCount: 5);

        // Act
        var player = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Players.Single();

        // Assert
        player.UserId.Should().Be(backdrop.UserId);
        player.Email.Should().NotBeNullOrWhiteSpace();
        player.FirstName.Should().Be("Ada");
        player.ExactScoreCount.Should().Be(3);
        player.CorrectResultCount.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAPlayerWhoPredictedNothing()
    {
        // Arrange - scored for the round, as everybody is, but never took part.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);

        await Seed.AddRoundResultAsync(roundId, backdrop.UserId, exactScoreCount: 0);

        // Act
        var player = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Players.Single();

        // Assert - whether that earns an email is a rule, so the row arrives with the count that decides it.
        player.PredictionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyTheirOwnPredictionsInThisRound()
    {
        // Arrange - two fixtures in this round and one in another, and a second player predicting alongside them.
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        var otherRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 13, Deadline.AddDays(7));

        var firstMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var secondMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);
        var otherRoundMatchId = await Seed.AddMatchAsync(otherRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        await Seed.AddRoundResultAsync(roundId, backdrop.UserId, exactScoreCount: 1);

        await Seed.AddPredictionAsync(firstMatchId, backdrop.UserId);
        await Seed.AddPredictionAsync(secondMatchId, backdrop.UserId);
        await Seed.AddPredictionAsync(otherRoundMatchId, backdrop.UserId);
        await Seed.AddPredictionAsync(firstMatchId, otherUserId);

        // Act
        var player = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Players.Single();

        // Assert
        player.PredictionCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAPlayerWhoWasNotScoredForTheRound()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddUserAsync("Grace", "Hopper");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        await Seed.AddRoundResultAsync(roundId, backdrop.UserId, exactScoreCount: 1);

        // Act
        var data = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - a round nobody has scored them for is not a round they can be told about.
        data.Players.Select(player => player.UserId).Should().Equal(backdrop.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnotherRoundsScoring()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        var otherRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 13, Deadline.AddDays(7));

        await Seed.AddRoundResultAsync(roundId, backdrop.UserId, exactScoreCount: 3);
        await Seed.AddRoundResultAsync(otherRoundId, backdrop.UserId, exactScoreCount: 9);

        // Act
        var player = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Players.Single();

        // Assert
        player.ExactScoreCount.Should().Be(3);
    }

    #endregion

    #region The memberships

    [Fact]
    public async Task ExecuteAsync_ShouldReturnApprovedMembershipsOfTheSeasonsLeaguesWithBothCachedPositions()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Alpha League");
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberStatsAsync(leagueId, backdrop.UserId, overallRank: 3, snapshotOverallRank: 5);

        // Act
        var membership = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Memberships.Single();

        // Assert - both positions arrive; the number of places moved is a rule.
        membership.UserId.Should().Be(backdrop.UserId);
        membership.LeagueId.Should().Be(leagueId);
        membership.LeagueName.Should().Be("Alpha League");
        membership.OverallRank.Should().Be(3);
        membership.SnapshotOverallRank.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAMembershipWithNoCachedPositionsYet()
    {
        // Arrange - a league whose ranking cache has not been written for this player.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        // Act
        var membership = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Memberships.Single();

        // Assert
        membership.OverallRank.Should().BeNull();
        membership.SnapshotOverallRank.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAMembershipThatWasNeverApproved()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, otherUserId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - asking to join is not joining, and this one is a scope rather than a rule: an email about a league
        // somebody has not been let into yet is not an email anybody would want sent.
        data.Memberships.Select(membership => membership.UserId).Should().Equal(backdrop.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnALeagueFromAnotherSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "This Season");
        var otherLeagueId = await Seed.AddLeagueAsync(otherSeasonId, backdrop.UserId, "Next Season");

        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(otherLeagueId, backdrop.UserId);

        // Act
        var data = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        data.Memberships.Select(membership => membership.LeagueId).Should().Equal(leagueId);
    }

    #endregion

    #region The league scores

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryPlayersPointsInEveryLeagueForTheRound()
    {
        // Arrange - two players in one league, one of whom is not being emailed at all.
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        await Seed.AddLeagueRoundResultAsync(leagueId, roundId, backdrop.UserId, basePoints: 20, boostedPoints: 30, appliedBoostCode: "NONE");
        await Seed.AddLeagueRoundResultAsync(leagueId, roundId, otherUserId, basePoints: 40, boostedPoints: 45, appliedBoostCode: "NONE");

        // Act
        var scores = (await Query.ExecuteAsync(roundId, CancellationToken.None)).LeagueScores;

        // Assert - everybody, because who topped the league is a rule and it needs the whole field. Both name parts
        // arrive because the abbreviation is a rule too, and the full name is what settles a tie.
        scores.Should().HaveCount(2);
        scores.Single(score => score.UserId == otherUserId).BoostedPoints.Should().Be(45);
        scores.Single(score => score.UserId == otherUserId).FirstName.Should().Be("Grace");
        scores.Single(score => score.UserId == otherUserId).LastName.Should().Be("Hopper");
        scores.Should().OnlyContain(score => score.LeagueId == leagueId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheBoostedPointsRatherThanTheBasePoints()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        await Seed.AddLeagueRoundResultAsync(leagueId, roundId, backdrop.UserId, basePoints: 20, boostedPoints: 40, appliedBoostCode: "DOUBLE");

        // Act
        var score = (await Query.ExecuteAsync(roundId, CancellationToken.None)).LeagueScores.Single();

        // Assert - what a player scored in a league is what they scored after their boost, which is the number the
        // league's own table shows.
        score.BoostedPoints.Should().Be(40);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnotherRoundsLeaguePoints()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 12, Deadline);
        var otherRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 13, Deadline.AddDays(7));
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        await Seed.AddLeagueRoundResultAsync(leagueId, roundId, backdrop.UserId, basePoints: 20, boostedPoints: 30, appliedBoostCode: "NONE");
        await Seed.AddLeagueRoundResultAsync(leagueId, otherRoundId, backdrop.UserId, basePoints: 60, boostedPoints: 70, appliedBoostCode: "NONE");

        // Act
        var score = (await Query.ExecuteAsync(roundId, CancellationToken.None)).LeagueScores.Single();

        // Assert
        score.BoostedPoints.Should().Be(30);
    }

    #endregion
}
