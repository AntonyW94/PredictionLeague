using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// A single league at <c>/leagues/{leagueId}/dashboard</c> - the page this whole suite was built for. A
/// leaderboard query whose <c>SELECT</c> had drifted from its result record threw here for a real user on
/// 2026-07-30, with the compiler happy and every unit test green, because a mocked read connection never runs
/// the SQL. Loading the page is what catches it.
/// </summary>
internal sealed class LeagueDashboardPage(IPage page)
{
    internal ILocator OverallLeaderboard => page.GetByTestId(TestIds.OverallLeaderboard);

    /// <summary>
    /// Rows across the overall leaderboard. Several elements share the id, which is what lets a journey ask
    /// both "did any render" and "how many".
    /// </summary>
    internal ILocator LeaderboardRows => page.GetByTestId(TestIds.LeaderboardRow);

    /// <summary>
    /// The countdown the page shows in place of its content while the competition has not started - when the
    /// entry deadline is still in the future, or no round has escaped Draft.
    /// </summary>
    /// <remarks>
    /// Asserted absent rather than ignored. Both of those conditions are the fixture's responsibility, so if
    /// this appears it means the arrangement is wrong, not the page - and saying that out loud beats timing
    /// out on a leaderboard that was never going to render.
    /// </remarks>
    internal ILocator NotStartedNotice => page.GetByTestId(TestIds.LeagueNotStarted);
}
