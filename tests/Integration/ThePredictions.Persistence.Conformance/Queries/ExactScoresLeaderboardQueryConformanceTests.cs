using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IExactScoresLeaderboardQuery"/> implementation must return.
///
/// The scoping is what matters here, and it is unusual: exact-score counts live in a global per-user-per-round
/// table, so they are scoped by the league's <b>season</b> rather than by the league. A member's total therefore
/// includes rounds played before they joined - existing behaviour, and pinned so a future adapter does not
/// "helpfully" narrow it to the league and quietly change everybody's totals.
/// </summary>
public abstract class ExactScoresLeaderboardQueryConformanceTests
{
    protected abstract IExactScoresLeaderboardQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnCountsPerRoundUnaggregated()
    {
        // Arrange - two scored rounds for one member.
        var world = await ArrangeAsync();
        await SeedExactScoresAsync(world, roundNumber: 1, exactScores: 2);
        await SeedExactScoresAsync(world, roundNumber: 2, exactScores: 3);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - two rows, not one total of five.
        data.ExactScores.Select(e => e.ExactScoreCount).Should().BeEquivalentTo([2, 3],
            "totalling them is a rule the handler owns.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoCounts_ForAMemberWhoHasNoneRecorded()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the member is present; counting them zero is the handler's rule.
        data.Members.Should().HaveCount(1);
        data.ExactScores.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyApprovedMembers_WithTheirNameParts()
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

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnCountsForSomeoneOutsideTheLeague()
    {
        // Arrange - a second player in the same season who is not in this league.
        var world = await ArrangeAsync();
        var outsiderUserId = await Seed.AddUserAsync("Grace", "Hopper");
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        await Seed.AddRoundResultAsync(roundId, outsiderUserId, exactScoreCount: 9);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data.ExactScores.Should().BeEmpty("the counts are season-scoped but still restricted to this league's members.");
    }

    private async Task SeedExactScoresAsync(ExactScoresWorld world, int roundNumber, int exactScores)
    {
        var roundId = await Seed.AddRoundAsync(world.SeasonId, roundNumber, DateTime.UtcNow.AddDays(-roundNumber));
        await Seed.AddRoundResultAsync(roundId, world.UserId, exactScores);
    }

    private async Task<ExactScoresWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new ExactScoresWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record ExactScoresWorld(int LeagueId, int SeasonId, string UserId);
}
