using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Persistence.SqlServer.Queries.Boosts;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>
/// <c>GetLeagueBoostUsageSummaryQueryHandler.GetUsagesAsync</c> hides another player's boost until that
/// round's deadline has passed:
///
/// <code>
/// AND (
///     ubu.[UserId] = @CurrentUserId
///     OR r.[DeadlineUtc] &lt;= GETUTCDATE()
/// )
/// </code>
///
/// Get it wrong and the page tells players what their opponents have played while there is still time to
/// react - the same class of fairness rule as the dashboard's prediction split, which is unit tested only
/// because it happens in C#. The shaping above this predicate was extracted to
/// <c>BoostUsageSummaryBuilder</c> and is unit tested; the censoring is not reachable that way, because a
/// mocked <c>IApplicationReadDbConnection</c> hands back rows that were never filtered.
///
/// The tests run through <c>Handle</c> rather than poking the private read, so what they assert is what a
/// player would actually see on the page. Deadlines are arranged relative to now because the predicate
/// calls <c>GETUTCDATE()</c> rather than taking a clock, so there is no instant to pin.
/// </summary>
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class BoostUsageSecrecyTests(SqlServerDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string BoostCode = "DOUBLE_POINTS";
    private const int OpenRoundNumber = 8;
    private const int ClosedRoundNumber = 7;

    [Fact]
    public async Task Handle_ShouldShowMyOwnBoost_WhenTheRoundDeadlineHasNotPassed()
    {
        // Arrange
        var world = await ArrangeLeagueWithBoostsPlayedInBothRoundsAsync();

        // Act
        var summary = await HandleAsAsync(world, world.MeUserId);

        // Assert - I can always see my own, open round or not.
        RoundsShownFor(summary, world.MeUserId).Should().BeEquivalentTo(new[] { ClosedRoundNumber, OpenRoundNumber });
    }

    [Fact]
    public async Task Handle_ShouldHideAnotherPlayersBoost_WhenTheRoundDeadlineHasNotPassed()
    {
        // Arrange - my opponent has played a boost in the open round and in the closed one.
        var world = await ArrangeLeagueWithBoostsPlayedInBothRoundsAsync();

        // Act
        var summary = await HandleAsAsync(world, world.MeUserId);

        // Assert - only the closed round's is mine to see. The open one is still theirs to change.
        RoundsShownFor(summary, world.OpponentUserId).Should().BeEquivalentTo(new[] { ClosedRoundNumber },
            $"round {OpenRoundNumber} has not locked, so revealing an opponent's boost would let me react to it.");
    }

    [Fact]
    public async Task Handle_ShouldShowAnotherPlayersBoost_WhenTheRoundDeadlineHasPassed()
    {
        // Arrange
        var world = await ArrangeLeagueWithBoostsPlayedInBothRoundsAsync();

        // Act
        var summary = await HandleAsAsync(world, world.MeUserId);

        // Assert - the secrecy is temporary, not permanent. Once the round has locked there is nothing
        // left to react to, and the table is meant to show who used theirs best.
        RoundsShownFor(summary, world.OpponentUserId).Should().Contain(ClosedRoundNumber);
    }

    [Fact]
    public async Task Handle_ShouldCensorFromTheViewersPerspective_WhenTheOtherPlayerLooks()
    {
        // Arrange - the same world seen by my opponent, for whom my open-round boost is now the secret
        // and their own is the one on show. Asserting from both sides rules out a predicate that happens
        // to be right for one user only - comparing against the wrong side of the join, say.
        var world = await ArrangeLeagueWithBoostsPlayedInBothRoundsAsync();

        // Act
        var summary = await HandleAsAsync(world, world.OpponentUserId);

        // Assert
        RoundsShownFor(summary, world.OpponentUserId).Should().BeEquivalentTo(new[] { ClosedRoundNumber, OpenRoundNumber });
        RoundsShownFor(summary, world.MeUserId).Should().BeEquivalentTo(new[] { ClosedRoundNumber });
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyTheVisibleBoostsAgainstTheAllowance_WhenOneIsHidden()
    {
        // Arrange - the censoring happens in SQL, so the remaining-uses figure the page shows is
        // computed from censored rows. That is the visible consequence of the rule, and it is a
        // deliberate one: telling me an opponent has one use left would leak that they have spent one.
        var world = await ArrangeLeagueWithBoostsPlayedInBothRoundsAsync();

        // Act
        var summary = await HandleAsAsync(world, world.MeUserId);

        // Assert - two uses per season; I have visibly spent both, my opponent only the locked one.
        UsageFor(summary, world.MeUserId).Remaining.Should().Be(0);
        UsageFor(summary, world.OpponentUserId).Remaining.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReportThePointsABoostWon_WhenTheRoundHasBeenScored()
    {
        // Arrange - PointsGained is the difference between boosted and base points, via a LEFT JOIN on
        // LeagueRoundResults and a CASE that yields NULL when the round has not been scored. Both arms
        // matter to the page, and neither runs under a mocked connection.
        var world = await ArrangeLeagueWithBoostsPlayedInBothRoundsAsync();

        // Act
        var summary = await HandleAsAsync(world, world.MeUserId);

        // Assert
        var myUsages = UsageFor(summary, world.MeUserId).Usages;

        myUsages.Single(u => u.RoundNumber == ClosedRoundNumber).PointsGained.Should().Be(9,
            "the closed round was scored: 18 boosted less 9 base.");
        myUsages.Single(u => u.RoundNumber == OpenRoundNumber).PointsGained.Should().BeNull(
            "the open round has no result row yet, so there are no points to report.");
    }

    #region Arrangement

    private async Task<BoostWorld> ArrangeLeagueWithBoostsPlayedInBothRoundsAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var meUserId = backdrop.UserId;
        var opponentUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, meUserId);
        await Seed.AddLeagueMemberAsync(leagueId, meUserId);
        await Seed.AddLeagueMemberAsync(leagueId, opponentUserId);

        var boostDefinitionId = await Seed.AddBoostDefinitionAsync(BoostCode, "Double Points");
        await Seed.AddLeagueBoostRuleAsync(leagueId, boostDefinitionId, totalUsesPerSeason: 2);

        // Relative to now, because the predicate reads GETUTCDATE() rather than an injected clock.
        var closedRoundId = await Seed.AddRoundAsync(
            backdrop.SeasonId, ClosedRoundNumber, deadlineUtc: DateTime.UtcNow.AddDays(-7));
        var openRoundId = await Seed.AddRoundAsync(
            backdrop.SeasonId, OpenRoundNumber, deadlineUtc: DateTime.UtcNow.AddDays(3));

        foreach (var userId in new[] { meUserId, opponentUserId })
        {
            await Seed.AddBoostUsageAsync(userId, leagueId, backdrop.SeasonId, closedRoundId, boostDefinitionId);
            await Seed.AddBoostUsageAsync(userId, leagueId, backdrop.SeasonId, openRoundId, boostDefinitionId);
        }

        // Only the closed round has been scored, so only its boost has points to report.
        await Seed.AddLeagueRoundResultAsync(leagueId, closedRoundId, meUserId, basePoints: 9, boostedPoints: 18, BoostCode);
        await Seed.AddLeagueRoundResultAsync(leagueId, closedRoundId, opponentUserId, basePoints: 4, boostedPoints: 8, BoostCode);

        return new BoostWorld(leagueId, meUserId, opponentUserId);
    }

    private async Task<List<BoostUsageSummaryDto>> HandleAsAsync(BoostWorld world, string currentUserId)
    {
        // The membership check is a separate class with its own SQL; substituting it keeps this test
        // about the secrecy predicate. The read connection is the real one.
        var membershipService = Substitute.For<ILeagueMembershipService>();

        var handler = new GetLeagueBoostUsageSummaryQueryHandler(
            new LeagueBoostUsageQuery(ReadDbConnection),
            membershipService,
            new TestDateTimeProvider(DateTime.UtcNow));

        return await handler.Handle(
            new GetLeagueBoostUsageSummaryQuery(world.LeagueId, currentUserId),
            CancellationToken.None);
    }

    private static PlayerWindowUsageDto UsageFor(List<BoostUsageSummaryDto> summary, string userId) =>
        summary.Single(b => b.BoostCode == BoostCode)
            .Windows.Single()
            .PlayerUsages.Single(p => p.UserId == userId);

    private static IEnumerable<int> RoundsShownFor(List<BoostUsageSummaryDto> summary, string userId) =>
        UsageFor(summary, userId).Usages.Select(u => u.RoundNumber);

    private sealed record BoostWorld(int LeagueId, string MeUserId, string OpponentUserId);

    #endregion
}
