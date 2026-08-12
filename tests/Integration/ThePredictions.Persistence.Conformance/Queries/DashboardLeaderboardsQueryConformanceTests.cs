using FluentAssertions;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IDashboardLeaderboardsQuery"/> implementation must return.
///
/// The tile shows several leagues at once, so the thing to pin is the scoping: only leagues the player is
/// approved in, everyone approved in those leagues rather than the player alone, and every row tagged with the
/// league it belongs to. An adapter that dropped the tagging would leave the handler unable to rank each league
/// separately, which is exactly what the old <c>PARTITION BY</c> was for.
/// </summary>
public abstract class DashboardLeaderboardsQueryConformanceTests
{
    protected abstract IDashboardLeaderboardsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThePlayerIsInNoLeagues()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Leagues.Should().BeEmpty();
        data.Members.Should().BeEmpty();
        data.Points.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyLeaguesThePlayerIsApprovedIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var joinedId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Joined");
        await Seed.AddLeagueMemberAsync(joinedId, backdrop.UserId);

        var pendingId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Still Pending");
        await Seed.AddLeagueMemberAsync(pendingId, backdrop.UserId, LeagueMemberStatus.Pending);

        await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Not A Member");

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Leagues.Select(league => league.LeagueName).Should().BeEquivalentTo(["Joined"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesNameStakeAndSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Joined");
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - the stake and the season's start date are both ordering facts for the tile.
        var league = data.Leagues.Single();
        league.LeagueId.Should().Be(leagueId);
        league.LeagueName.Should().Be("Joined");
        league.SeasonName.Should().Be("2026/27");
        // The rounds that exist, not the number the season declares - which is what "is the season over" is now decided
        // from everywhere. These worlds seed no rounds, so the count is nought rather than the season's 38.
        league.SeasonRoundCount.Should().Be(0);
        league.SeasonStartDateUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyTheSeasonsCompletedRounds()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-3), RoundStatus.Completed);
        await Seed.AddRoundAsync(backdrop.SeasonId, 2, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        await Seed.AddRoundAsync(backdrop.SeasonId, 3, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);
        await Seed.AddRoundAsync(backdrop.SeasonId, 4, DateTime.UtcNow.AddDays(1));

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - the count is a fact; whether it means the season is over is SeasonCompletion.IsFinished.
        data.Leagues.Single().CompletedRoundCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportARoundUnderWay()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Leagues.Single().HasRoundInProgress.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportNoRoundUnderWay_WhenNoneIsInProgress()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Leagues.Single().HasRoundInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryApprovedMemberOfTheLeague_NotJustThePlayer()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(leagueId, rivalId);

        var pendingId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(leagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - the tile shows whole tables, but only the approved rows of them.
        data.Members.Select(member => (member.FirstName, member.LastName))
            .Should().BeEquivalentTo([("Ada", "Lovelace"), ("Grace", "Hopper")]);
        data.Members.Should().OnlyContain(member => member.LeagueId == leagueId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoMembers_OfALeagueThePlayerIsNotIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var mineId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Mine");
        await Seed.AddLeagueMemberAsync(mineId, backdrop.UserId);

        var strangerId = await Seed.AddUserAsync("Grace", "Hopper");
        var theirsId = await Seed.AddLeagueAsync(backdrop.SeasonId, strangerId, "Theirs");
        await Seed.AddLeagueMemberAsync(theirsId, strangerId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Members.Should().OnlyContain(member => member.LeagueId == mineId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPointsTaggedWithTheirLeague()
    {
        // Arrange - the same player scoring in two of their leagues.
        var backdrop = await Seed.AddBackdropAsync();
        var firstId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "First");
        await Seed.AddLeagueMemberAsync(firstId, backdrop.UserId);
        var secondId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Second");
        await Seed.AddLeagueMemberAsync(secondId, backdrop.UserId);

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        await Seed.AddLeagueRoundResultAsync(firstId, roundId, backdrop.UserId, 9, 18, "DOUBLE");
        await Seed.AddLeagueRoundResultAsync(secondId, roundId, backdrop.UserId, 9, 9, "NONE");

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - without the league id the handler could not rank the two tables separately.
        data.Points.Single(row => row.LeagueId == firstId).BoostedPoints.Should().Be(18);
        data.Points.Single(row => row.LeagueId == secondId).BoostedPoints.Should().Be(9);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsPointsSeparately()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        var firstRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-2));
        var secondRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, DateTime.UtcNow.AddDays(-1));
        await Seed.AddLeagueRoundResultAsync(leagueId, firstRoundId, backdrop.UserId, 5, 10, "DOUBLE");
        await Seed.AddLeagueRoundResultAsync(leagueId, secondRoundId, backdrop.UserId, 7, 7, "NONE");

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - summing is the handler's job, so the rows arrive unaggregated.
        data.Points.Select(row => row.BoostedPoints).Should().BeEquivalentTo([10, 7]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoPoints_ForALeagueThePlayerIsNotIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var mineId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Mine");
        await Seed.AddLeagueMemberAsync(mineId, backdrop.UserId);

        var strangerId = await Seed.AddUserAsync("Grace", "Hopper");
        var theirsId = await Seed.AddLeagueAsync(backdrop.SeasonId, strangerId, "Theirs");
        await Seed.AddLeagueMemberAsync(theirsId, strangerId);

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        await Seed.AddLeagueRoundResultAsync(theirsId, roundId, strangerId, 9, 18, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Points.Should().BeEmpty();
    }
}
