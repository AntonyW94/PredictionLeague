using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The player's dashboard at <c>/dashboard</c>. Only the parts a login journey needs are here; the tiles come
/// when a journey exists that looks at them.
/// </summary>
/// <remarks>
/// The account menu and the error panel used to live here and have moved to <see cref="SiteLayout"/>, where
/// they belong - the navigation bar and the shared <c>ApiError</c> component are on every page, so filing
/// them under the dashboard implied a relationship that does not exist.
/// </remarks>
internal sealed class DashboardPage(IPage page)
{
    /// <summary>
    /// Wraps both shapes the dashboard can take - the tiles, and the onboarding takeover a user without a
    /// Season Pass or a league gets - so it is what proves the page itself rendered rather than which version
    /// of it appeared. The id is deliberately on both branches in the markup.
    /// </summary>
    internal ILocator Container => page.GetByTestId(TestIds.Dashboard);

    /// <summary>
    /// Opens the first league on the My Leagues carousel.
    /// </summary>
    /// <remarks>
    /// <c>.First</c> because a player can belong to several and the carousel renders them all - the seeded
    /// player belongs to exactly one, so first is the only one, and the locator stays honest if that changes.
    /// </remarks>
    internal async Task OpenFirstLeagueAsync()
    {
        var view = page.GetByTestId(TestIds.MyLeaguesView).First;

        await view.ShouldBeVisibleAsync();
        await view.ClickAsync();

        await page.WaitForURLAsync(url =>
            url.Contains("/leagues/", StringComparison.Ordinal)
            && url.EndsWith("/dashboard", StringComparison.Ordinal));
    }
}
