using FluentAssertions;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IMyLeaguesQuery"/> implementation must return.
///
/// The statement this replaced chose the active round, counted wins and worked out the pot on its way out. None of
/// that may happen here - and in particular the rounds must come back unfiltered, drafts included, because which
/// round the tile is about is a rule with a forty-eight hour grace period in it that the write path also depends on.
/// </summary>
public abstract class MyLeaguesQueryConformanceTests
{
    protected abstract IMyLeaguesQuery Query { get; }

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
        data.SeasonRounds.Should().BeEmpty();
        data.RoundScores.Should().BeEmpty();
        data.Stats.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyLeaguesThePlayerIsApprovedIn()
    {
        // Arrange
        var world = await ArrangeAsync();

        var pendingId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Still Pending");
        await Seed.AddLeagueMemberAsync(pendingId, world.UserId, LeagueMemberStatus.Pending);

        await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Not A Member");

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        data.Leagues.Select(league => league.LeagueId).Should().BeEquivalentTo([world.LeagueId]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesSeasonAndCompetition()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        var league = data.Leagues.Single();
        league.SeasonId.Should().Be(world.SeasonId);
        league.SeasonName.Should().Be("2026/27");
        league.NumberOfRounds.Should().Be(38);
        league.SeasonStartDateUtc.Should().NotBe(default);
        league.CompetitionType.Should().BeOneOf(CompetitionType.League, CompetitionType.Tournament);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheLeaguesApprovedMembers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        var pendingId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - the count multiplies the entry fee into the prize pot, so a pending member must not inflate it.
        data.Leagues.Single().MemberCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheSeasonsCompletedRounds()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - whether that means the league is over is the handler's rule.
        data.Leagues.Single().CompletedRoundCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTotalWhatHasBeenPaidOutAndWhatThePlayerHasWon()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 10m);
        await Seed.AddWinningAsync(world.UserId, settingId, 10m, roundNumber: 1);
        await Seed.AddWinningAsync(rivalId, settingId, 25m, roundNumber: 2);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - the pot's arithmetic is the handler's, but these two sums are its inputs.
        var league = data.Leagues.Single();
        league.TotalPaidOut.Should().Be(35m);
        league.UserWinnings.Should().Be(10m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfTheSeason_DraftsIncluded()
    {
        // Arrange
        var world = await ArrangeAsync();
        var draftId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(7), RoundStatus.Draft);
        var publishedId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(14));

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - that a draft can never be the active round is a rule, and the handler applies it.
        data.SeasonRounds.Select(round => round.RoundId).Should().BeEquivalentTo([draftId, publishedId]);
        data.SeasonRounds.Single(round => round.RoundId == draftId).Status.Should().Be(RoundStatus.Draft);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsCompletionDate()
    {
        // Arrange
        var world = await ArrangeAsync();
        var completedDateUtc = DateTime.UtcNow.AddHours(-6);
        await Seed.AddRoundAsync(
            world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed, completedDateUtc: completedDateUtc);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - the forty-eight hour grace period is measured from this, and it is not the same as the status.
        data.SeasonRounds.Single().CompletedDateUtc.Should().BeCloseTo(completedDateUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoCompletionDate_ForARoundThatHasNotFinished()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        data.SeasonRounds.Single().CompletedDateUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheRoundsMatchesByState()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);
        await Seed.AddMatchAsync(roundId, world.RoundHomeTeamId, world.RoundAwayTeamId, status: MatchStatus.InProgress);
        await Seed.AddMatchAsync(roundId, world.RoundHomeTeamId, world.RoundAwayTeamId, status: MatchStatus.Completed);
        await Seed.AddMatchAsync(roundId, world.RoundHomeTeamId, world.RoundAwayTeamId, status: MatchStatus.Completed);
        await Seed.AddMatchAsync(roundId, world.RoundHomeTeamId, world.RoundAwayTeamId, status: MatchStatus.Scheduled);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - these decide whether the tile shows a moving arrow during a live round.
        var round = data.SeasonRounds.Single();
        round.InProgressMatchCount.Should().Be(1);
        round.CompletedMatchCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheStageTextRaw_OrNothingWhereThereIsNoMapping()
    {
        // Arrange
        var world = await ArrangeAsync();
        var mappedId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2));
        await Seed.AddTournamentRoundMappingAsync(world.SeasonId, 1, "Group Stage");
        var unmappedId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1));

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - an unmapped round shows no stage, which is not the same as a knockout round.
        data.SeasonRounds.Single(round => round.RoundId == mappedId).Stages.Should().Be("Group Stage");
        data.SeasonRounds.Single(round => round.RoundId == unmappedId).Stages.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryMembersScoresTaggedWithLeagueAndRound()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);

        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, roundId, world.UserId, 9, 18, "DOUBLE");
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, roundId, rivalId, 7, 7, "NONE");

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - a round cannot be known to be won without the scores it was won against.
        data.RoundScores.Should().HaveCount(2);
        data.RoundScores.Should().OnlyContain(score => score.LeagueId == world.LeagueId && score.RoundId == roundId);
        data.RoundScores.Single(score => score.UserId == rivalId).BoostedPoints.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoScores_FromALeagueThePlayerIsNotIn()
    {
        // Arrange
        var world = await ArrangeAsync();
        var strangerId = await Seed.AddUserAsync("Grace", "Hopper");
        var theirLeagueId = await Seed.AddLeagueAsync(world.SeasonId, strangerId, "Theirs");
        await Seed.AddLeagueMemberAsync(theirLeagueId, strangerId);

        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddLeagueRoundResultAsync(theirLeagueId, roundId, strangerId, 9, 18, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        data.RoundScores.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnThePlayersCachedRanks()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddLeagueMemberStatsAsync(world.LeagueId, world.UserId, overallRank: 3, monthRank: 5);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - read from the cache, never recomputed. ADR-0015.
        var stats = data.Stats.Single();
        stats.LeagueId.Should().Be(world.LeagueId);
        stats.OverallRank.Should().Be(3);
        stats.MonthRank.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoCachedRanks_ForAnotherPlayer()
    {
        // Arrange
        var world = await ArrangeAsync();
        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, rivalId);
        await Seed.AddLeagueMemberStatsAsync(world.LeagueId, rivalId, overallRank: 1);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - the tile shows the player's own standing, not the whole league's.
        data.Stats.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnACachedRankAsNull_WhenThePositionDoesNotExist()
    {
        // Arrange - a stats row exists but the pre-round columns are empty.
        var world = await ArrangeAsync();
        await Seed.AddLeagueMemberStatsAsync(world.LeagueId, world.UserId, overallRank: 3);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - null is meaningful: it is what suppresses the change arrow, so it must not arrive as a zero.
        data.Stats.Single().SnapshotOverallRank.Should().BeNull();
    }

    private async Task<MyLeaguesWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new MyLeaguesWorld(
            leagueId, backdrop.SeasonId, backdrop.UserId, backdrop.HomeTeamId, backdrop.AwayTeamId);
    }

    private sealed record MyLeaguesWorld(
        int LeagueId,
        int SeasonId,
        string UserId,
        int RoundHomeTeamId,
        int RoundAwayTeamId);
}
