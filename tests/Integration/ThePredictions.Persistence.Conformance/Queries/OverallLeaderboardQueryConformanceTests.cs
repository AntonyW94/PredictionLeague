using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IOverallLeaderboardQuery"/> implementation must return.
///
/// Note what is absent: no assertion about totals, positions, names or order. The port promises raw facts, and
/// an adapter that helpfully summed or ranked would be re-implementing rules the handler owns - the tie policy
/// most of all, where two implementations disagreeing would show players different positions depending on which
/// database answered.
/// </summary>
public abstract class OverallLeaderboardQueryConformanceTests
{
    protected abstract IOverallLeaderboardQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyApprovedMembers_WithTheirNameParts()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var pendingUserId = await Seed.AddUserAsync("Grace", "Hopper");
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, pendingUserId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert - name parts, not a formatted name: formatting is a C# rule.
        data.Members.Select(m => (m.FirstName, m.LastName)).Should().BeEquivalentTo([("Ada", "Lovelace")]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRoundPointsUnaggregated_SoTheTotalIsTheCallersToCompute()
    {
        // Arrange - three scored rounds for one member.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        foreach (var (roundNumber, points) in new[] { (1, 9), (2, 12), (3, 4) })
        {
            var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, roundNumber, DateTime.UtcNow.AddDays(-roundNumber));
            await Seed.AddLeagueRoundResultAsync(leagueId, roundId, backdrop.UserId, points, points, "NONE");
        }

        // Act
        var data = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert - three rows, not one total of 25.
        data.RoundPoints.Where(p => p.UserId == backdrop.UserId).Select(p => p.BoostedPoints)
            .Should().BeEquivalentTo([9, 12, 4],
                "the port returns rows; summing them is a rule the handler owns.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoPointsRows_ForAMemberWhoHasPlayedNothing()
    {
        // Arrange - the member exists, with no results at all.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        // Act
        var data = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert - the member is present; scoring them zero is the handler's rule, not an empty result here.
        data.Members.Should().HaveCount(1);
        data.RoundPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportWhetherTheSeasonHasACompletedRound()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Published);

        // Act
        var beforeAnyCompleted = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert
        beforeAnyCompleted.HasCompletedRound.Should().BeFalse();

        // Arrange - now complete one.
        await Seed.AddRoundAsync(backdrop.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);

        // Act
        var afterCompleted = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert
        afterCompleted.HasCompletedRound.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportWhetherARoundIsInProgress()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress);

        // Act
        var data = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert
        data.HasRoundInProgress.Should().BeTrue();
        data.HasCompletedRound.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForALeagueWithNoMembers()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        var data = await Query.ExecuteAsync(leagueId, CancellationToken.None);

        data.Members.Should().BeEmpty();
    }
}
