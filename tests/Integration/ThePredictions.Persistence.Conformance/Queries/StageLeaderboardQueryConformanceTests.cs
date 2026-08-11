using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IStageLeaderboardQuery"/> implementation must return.
///
/// The thing to pin is that the stage text comes back <b>raw</b>. Classifying it used to be a
/// <c>CASE</c> over a <c>LIKE</c> whose behaviour depended on the collation being case-insensitive; an adapter
/// that classified it here would put that dependency straight back, and two adapters could then disagree about
/// which half of a tournament a round belongs to.
/// </summary>
public abstract class StageLeaderboardQueryConformanceTests
{
    protected abstract IStageLeaderboardQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheStageTextRaw_NotAClassification()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        await Seed.AddTournamentRoundMappingAsync(world.SeasonId, 1, "Group Stage");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data.SeasonRounds.Single(r => r.RoundId == roundId).Stages
            .Should().Be("Group Stage", "classifying it is the handler's rule.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoStageText_ForARoundWithNoTournamentMapping()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - null rather than absent, so the handler can classify the round rather than lose it.
        data.SeasonRounds.Single(r => r.RoundId == roundId).Stages.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundInTheSeason_NotOnlyOneStages()
    {
        // Arrange - one group round, one knockout, one unmapped.
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-3));
        await Seed.AddTournamentRoundMappingAsync(world.SeasonId, 1, "Group Stage");
        await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-2));
        await Seed.AddTournamentRoundMappingAsync(world.SeasonId, 2, "Quarter-finals");
        await Seed.AddRoundAsync(world.SeasonId, 3, DateTime.UtcNow.AddDays(-1));

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the port does not filter by stage; the handler does.
        data.SeasonRounds.Should().HaveCount(3);
        data.SeasonRounds.Select(r => r.Stages).Should().BeEquivalentTo(["Group Stage", "Quarter-finals", null]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsStatus()
    {
        // Arrange
        var world = await ArrangeAsync();
        var completedId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        var liveId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - status drives both which points count towards the pre-round position and whether it shows.
        data.SeasonRounds.Single(r => r.RoundId == completedId).Status.Should().Be(RoundStatus.Completed);
        data.SeasonRounds.Single(r => r.RoundId == liveId).Status.Should().Be(RoundStatus.InProgress);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPointsIdentifiedByRound()
    {
        // Arrange
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, roundId, world.UserId, 9, 18, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the round id is what lets the handler filter by stage and exclude the live round.
        var points = data.RoundPoints.Single();
        points.RoundId.Should().Be(roundId);
        points.BoostedPoints.Should().Be(18);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyApprovedMembers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingUserId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data.Members.Select(m => (m.FirstName, m.LastName)).Should().BeEquivalentTo([("Ada", "Lovelace")]);
    }

    private async Task<StageWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new StageWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record StageWorld(int LeagueId, int SeasonId, string UserId);
}
