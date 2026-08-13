using Microsoft.Playwright;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// A single league at <c>/leagues/{leagueId}/dashboard</c> - the page this whole stage exists for. A
/// leaderboard query whose <c>SELECT</c> had drifted from its result record threw here for a real user on
/// 2026-07-30, with the compiler happy and every unit test green, because a mocked read connection never
/// runs the SQL. Loading the page is what catches it.
/// </summary>
internal sealed class LeagueDashboardPage(IPage page)
{
    /// <summary>
    /// Exact, because a role name match is a substring match by default and the finished layout puts the
    /// season recap and league records tiles on the same page - a looser match risks two hits and a
    /// strict-mode violation, which would read as a broken page rather than a broken selector.
    /// </summary>
    internal ILocator OverallLeaderboardHeading =>
        page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Overall Leaderboard", Exact = true });

    /// <summary>
    /// Any of the shapes the page settles into once "Loading dashboard..." clears: the pre-season notice, a
    /// rendered leaderboard, or the overall tile showing its nothing-scored-yet message. Waiting on the set
    /// rather than on the leaderboard alone means an unusable fixture can be reported as such, instead of
    /// timing out on a tile that was never going to appear.
    /// </summary>
    internal ILocator SettledContent =>
        page.Locator(".dashboard-waiting, .section:has-text('Overall Leaderboard')");

    /// <summary>
    /// Rows across every leaderboard tile on the page. Not scoped to the overall tile: the finished and
    /// in-progress layouts order the tiles differently, and any rendered row proves the read materialised.
    /// </summary>
    internal ILocator LeaderboardRows => page.Locator("table.leaderboard-table tbody tr");

    internal ILocator ErrorMessages => page.Locator(".message-box-solid.error");

    /// <summary>
    /// Shown in place of the leaderboards when the league's season has not started. It is asserted against
    /// rather than ignored, because dev is refreshed from production and a future refresh could make the
    /// first league a pre-season one - which should read as an unusable fixture, not as a broken page.
    /// </summary>
    internal ILocator PreSeasonNotice => page.Locator(".dashboard-waiting");
}
