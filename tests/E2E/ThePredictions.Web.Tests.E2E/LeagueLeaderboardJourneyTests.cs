using AwesomeAssertions;
using ThePredictions.Web.Tests.E2E.Harness;
using ThePredictions.Web.Tests.E2E.Pages;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// The journey this suite exists for: a player opens their league and a leaderboard renders.
///
/// On 30 July 2026 a leaderboard query's <c>SELECT</c> had drifted from its result record and the page threw
/// for a real user at 07:04 UTC. The compiler was happy, all 1,647 unit tests were green, and it was found by
/// reading logs after the fact - because handler tests mock the read connection, so no SQL ever ran. This
/// test loads the page.
/// </summary>
/// <remarks>
/// Arranges its own season, league, player and round in <see cref="InitializeAsync"/> rather than sharing an
/// arrangement with other classes. See <c>TestDatabase.SeedLeagueAsync</c> for why that is per class.
/// </remarks>
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public class LeagueLeaderboardJourneyTests(StackFixture stack) : E2ETestBase(stack), IAsyncLifetime
{
    private SeededLeague _league = null!;

    public async ValueTask InitializeAsync() =>
        _league = await Database.SeedLeagueAsync(nameof(LeagueLeaderboardJourneyTests));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Player_ShouldSeeALeaderboard_WhenTheyOpenTheirLeague()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(_league.PlayerEmail, _league.PlayerPassword);

        var dashboard = new DashboardPage(page);
        var layout = new SiteLayout(page);

        // The settled dashboard rather than the onboarding takeover, which is what the seeded Season Pass
        // buys: without one, `get-pass` is an outstanding required step and the tiles are replaced wholesale
        // by the checklist - so there would be no league to click through to.
        await dashboard.Container.ShouldBeVisibleAsync();

        await dashboard.OpenFirstLeagueAsync();

        var league = new LeagueDashboardPage(page);

        await league.OverallLeaderboard.ShouldBeVisibleAsync();

        // Checked before the rows, so a mis-seeded league reports itself instead of timing out on a tile that
        // was never going to appear.
        await league.NotStartedNotice.ShouldNotExistAsync();

        await league.LeaderboardRows.First.ShouldBeVisibleAsync();

        // The seeded player is the league's only approved member, and no results have been posted - so this
        // is exactly one row, scoring zero. That is the interesting case rather than a weak one: the handler
        // deliberately gives a member with no results a position rather than leaving them off the table, and
        // asserting one row pins that behaviour where "at least one" would not.
        (await league.LeaderboardRows.CountAsync()).Should().Be(1,
            "the league has one approved member and no posted results, so the leaderboard should show that "
            + "member on zero points - a member with no results takes a position rather than being omitted.");

        await layout.ErrorMessages.ShouldNotExistAsync();
    }
}
