using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueRoundResultsQuery"/> implementation must return.
///
/// The theme is that nothing may be interpreted on the way out. The statement this replaced hid predictions,
/// ranked members, zero-filled points and dropped postponed fixtures, all inside one SELECT - so an adapter that
/// helpfully did any of that again would take the rules back out of reach of a test. In particular no
/// implementation may consult the database's clock: the round's deadline and each fixture's lock time come back
/// as data, and the comparison happens in C# against an injected instant.
/// </summary>
public abstract class LeagueRoundResultsQueryConformanceTests
{
    protected abstract ILeagueRoundResultsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheRoundDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId + 5_000, CancellationToken.None);

        // Assert
        data.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheRoundsDeadline()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert - the deadline is half of the secrecy rule; without it nothing could be hidden.
        data!.Round.DeadlineUtc.Should().BeCloseTo(world.DeadlineUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryFixtureInTheRound_IncludingPostponedOnes()
    {
        // Arrange
        var world = await ArrangeAsync();
        var playedId = await Seed.AddMatchAsync(world.RoundId, world.HomeTeamId, world.AwayTeamId);
        var calledOffId = await Seed.AddMatchAsync(
            world.RoundId, world.HomeTeamId, world.AwayTeamId, status: MatchStatus.Postponed);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert - which fixtures belong on the grid is Match.IsPostponed, not a WHERE clause.
        data!.Round.Matches.Select(match => match.Id).Should().BeEquivalentTo([playedId, calledOffId]);
        data.Round.Matches.Single(match => match.Id == calledOffId).Status.Should().Be(MatchStatus.Postponed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachFixturesCustomLockTime()
    {
        // Arrange
        var world = await ArrangeAsync();
        var lockTimeUtc = world.DeadlineUtc.AddHours(-2);
        var matchId = await Seed.AddMatchAsync(
            world.RoundId, world.HomeTeamId, world.AwayTeamId, customLockTimeUtc: lockTimeUtc);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert - a custom lock time can reveal one fixture's predictions while the round is still open.
        data!.Round.Matches.Single(match => match.Id == matchId).CustomLockTimeUtc
            .Should().BeCloseTo(lockTimeUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyApprovedMembers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingUserId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert
        data!.Members.Select(member => (member.FirstName, member.LastName))
            .Should().BeEquivalentTo([("Ada", "Lovelace")]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachPredictionWithItsScoresAndOutcome()
    {
        // Arrange
        var world = await ArrangeAsync();
        var matchId = await Seed.AddMatchAsync(world.RoundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddPredictionAsync(matchId, world.UserId, 3, 1, PredictionOutcome.ExactScore);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert
        var prediction = data!.Predictions.Single();
        prediction.UserId.Should().Be(world.UserId);
        prediction.MatchId.Should().Be(matchId);
        prediction.PredictedHomeScore.Should().Be(3);
        prediction.PredictedAwayScore.Should().Be(1);
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoPredictions_ForFixturesInAnotherRound()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherRoundId = await Seed.AddRoundAsync(world.SeasonId, 2, world.DeadlineUtc.AddDays(7));
        var otherMatchId = await Seed.AddMatchAsync(otherRoundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddPredictionAsync(otherMatchId, world.UserId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert
        data!.Predictions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoPredictions_ForPlayersOutsideTheLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var matchId = await Seed.AddMatchAsync(world.RoundId, world.HomeTeamId, world.AwayTeamId);
        var outsiderId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddPredictionAsync(matchId, outsiderId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert - a prediction by someone who is not in this league cannot appear on its grid.
        data!.Predictions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPointsOnlyWhereAResultRowExists()
    {
        // Arrange - two members, one scored.
        var world = await ArrangeAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, otherUserId);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, world.RoundId, world.UserId, 9, 18, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert - the missing member scores zero, and that is the handler's rule rather than a COALESCE here.
        data!.Points.Should().HaveCount(1);
        data.Points.Single().UserId.Should().Be(world.UserId);
        data.Points.Single().BoostedPoints.Should().Be(18);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPointsForThisLeagueOnly()
    {
        // Arrange - the same player scoring in a second league in the same season.
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        await Seed.AddLeagueMemberAsync(otherLeagueId, world.UserId);
        await Seed.AddLeagueRoundResultAsync(otherLeagueId, world.RoundId, world.UserId, 40, 80, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert
        data!.Points.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheBoostPlayedWithItsArtwork()
    {
        // Arrange
        var world = await ArrangeAsync();
        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        await Seed.AddBoostUsageAsync(world.UserId, world.LeagueId, world.SeasonId, world.RoundId, boostId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert
        var usage = data!.BoostUsages.Single();
        usage.UserId.Should().Be(world.UserId);
        usage.Code.Should().Be("DOUBLE");
        usage.ImageUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoBoosts_ForAnotherLeaguesUsage()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var boostId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        await Seed.AddBoostUsageAsync(world.UserId, otherLeagueId, world.SeasonId, world.RoundId, boostId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.RoundId, CancellationToken.None);

        // Assert
        data!.BoostUsages.Should().BeEmpty();
    }

    private async Task<RoundResultsWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        var deadlineUtc = DateTime.UtcNow.AddDays(1);
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, deadlineUtc);

        return new RoundResultsWorld(
            leagueId, backdrop.SeasonId, roundId, backdrop.UserId, backdrop.HomeTeamId, backdrop.AwayTeamId, deadlineUtc);
    }

    private sealed record RoundResultsWorld(
        int LeagueId,
        int SeasonId,
        int RoundId,
        string UserId,
        int HomeTeamId,
        int AwayTeamId,
        DateTime DeadlineUtc);
}
