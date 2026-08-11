using FluentAssertions;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IActiveRoundsQuery"/> implementation must return.
///
/// Three obligations that are easy to get subtly wrong. Postponed matches must be left out, because the round's prediction
/// deadline is worked out from these rows and a called-off match must not hold a round open. The confirmed-teams flag must look
/// at every match <b>including</b> postponed ones, which is what the old statement did. And the prediction split must be the
/// counts across every player, classified by scoreline - the one rule this port still carries, and therefore the one that needs
/// pinning here rather than in a handler test.
/// </summary>
public abstract class ActiveRoundsQueryConformanceTests
{
    protected abstract IActiveRoundsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForAPlayerInNoLeagues()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(1));

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - the dashboard shows rounds of seasons the player is actually competing in.
        data.Rounds.Should().BeEmpty();
        data.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnARoundOfASeasonThePlayerCompetesIn()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        var round = data.Rounds.Single();
        round.RoundId.Should().Be(roundId);
        round.SeasonName.Should().Be("2026/27");
        round.RoundNumber.Should().Be(1);
    }

    [Theory]
    [InlineData(RoundStatus.Draft)]
    [InlineData(RoundStatus.Completed)]
    public async Task ExecuteAsync_ShouldNotReturnADraftOrFinishedRound(RoundStatus status)
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1), status);
        await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - a draft is not yet visible to players, and a finished round belongs to the results pages.
        data.Rounds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RoundStatus.Published)]
    [InlineData(RoundStatus.InProgress)]
    public async Task ExecuteAsync_ShouldReturnARoundThatIsPublishedOrUnderWay(RoundStatus status)
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1), status);
        await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        data.Rounds.Single().Status.Should().Be(status);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportWhetherThePlayerHasPredicted()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        var matchId = await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddPredictionAsync(matchId, world.UserId);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - this is what keeps a finished round on the tile for somebody who took part.
        data.Rounds.Single().HasUserPredicted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountSomebodyElsesPredictionAsThePlayers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        var matchId = await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);

        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddPredictionAsync(matchId, rivalId);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        data.Rounds.Single().HasUserPredicted.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportConfirmedTeams_EvenWhenTheOnlyConfirmedMatchIsPostponed()
    {
        // Arrange - the subtle one. The flag looks at every match; the returned matches exclude postponed ones.
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        await Seed.AddMatchAsync(
            roundId, world.HomeTeamId, world.AwayTeamId, status: MatchStatus.Postponed);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - working the flag out from the returned rows would drop this round entirely.
        data.Rounds.Single().HasConfirmedMatch.Should().BeTrue();
        data.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportNoConfirmedTeams_ForARoundOfPlaceholders()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        await Seed.AddMatchAsync(roundId, homeTeamId: null, awayTeamId: null);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        data.Rounds.Single().HasConfirmedMatch.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAPostponedMatch()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        var playedId = await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId, status: MatchStatus.Postponed);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - not cosmetic: the round's latest prediction deadline is worked out from these rows, and a called-off match
        // must not hold a round open.
        data.Matches.Select(match => match.RoundId).Should().Equal(roundId);
        data.Matches.Should().HaveCount(1);
        playedId.Should().BePositive();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachMatchesLockTimeAndKickOff()
    {
        // Arrange
        var world = await ArrangeAsync();
        var kickOff = DateTime.UtcNow.AddDays(2);
        var lockTime = DateTime.UtcNow.AddDays(1);
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddHours(12));
        await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId, kickOff, lockTime);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - the lock time drives both the round's deadline and whether the split may be shown.
        var match = data.Matches.Single();
        match.MatchDateTimeUtc.Should().BeCloseTo(kickOff, TimeSpan.FromSeconds(1));
        match.CustomLockTimeUtc.Should().BeCloseTo(lockTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoLockTime_ForAMatchWithoutOne()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - null means "use the round's deadline", which is the handler's rule.
        data.Matches.Single().CustomLockTimeUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSplitEveryPlayersPredictionsByWhoTheyBacked()
    {
        // Arrange - four predictions on one match: two home wins, one draw, one away win.
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        var matchId = await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);

        await Seed.AddPredictionAsync(matchId, world.UserId, homeScore: 3, awayScore: 1);

        var secondId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddPredictionAsync(matchId, secondId, homeScore: 1, awayScore: 0);

        var thirdId = await Seed.AddUserAsync("Alan", "Turing");
        await Seed.AddPredictionAsync(matchId, thirdId, homeScore: 2, awayScore: 2);

        var fourthId = await Seed.AddUserAsync("Edsger", "Dijkstra");
        await Seed.AddPredictionAsync(matchId, fourthId, homeScore: 0, awayScore: 2);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - the classification stays in the adapter, so this is where it has to be pinned: a higher home score is a
        // home backing, equal scores a draw, a higher away score an away backing.
        var match = data.Matches.Single();
        match.HomeCount.Should().Be(2);
        match.DrawCount.Should().Be(1);
        match.AwayCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnThePlayersOwnPredictionAlongsideTheSplit()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(1));
        var matchId = await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddPredictionAsync(matchId, world.UserId, homeScore: 3, awayScore: 1);

        var rivalId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddPredictionAsync(matchId, rivalId, homeScore: 0, awayScore: 4);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert - one row per match, carrying this player's scoreline and nobody else's.
        var match = data.Matches.Single();
        match.PredictedHomeScore.Should().Be(3);
        match.PredictedAwayScore.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnARoundOfAnInactiveSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var inactiveSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2020/21", isActive: false);
        var leagueId = await Seed.AddLeagueAsync(inactiveSeasonId, world.UserId, "Old League");
        await Seed.AddLeagueMemberAsync(leagueId, world.UserId);

        var roundId = await Seed.AddRoundAsync(inactiveSeasonId, 1, DateTime.UtcNow.AddDays(1));
        await Seed.AddMatchAsync(roundId, world.HomeTeamId, world.AwayTeamId);

        // Act
        var data = await Query.ExecuteAsync(world.UserId, CancellationToken.None);

        // Assert
        data.Rounds.Should().BeEmpty();
    }

    private async Task<ActiveRoundsWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new ActiveRoundsWorld(
            backdrop.CompetitionId, backdrop.SeasonId, backdrop.UserId, backdrop.HomeTeamId, backdrop.AwayTeamId);
    }

    private sealed record ActiveRoundsWorld(
        int CompetitionId,
        int SeasonId,
        string UserId,
        int HomeTeamId,
        int AwayTeamId);
}
