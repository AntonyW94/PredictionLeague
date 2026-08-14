using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The player's dashboard at <c>/dashboard</c>. Only the parts a login journey needs are here; the tiles
/// come when a journey exists that looks at them.
/// </summary>
/// <remarks>
/// Every element is addressed by its <c>data-test-id</c> and nothing else - see <see cref="TestIds"/>.
/// </remarks>
internal sealed class DashboardPage(IPage page)
{
    /// <summary>
    /// Wraps both shapes the dashboard can take - the tiles, and the onboarding takeover a user without a
    /// Season Pass or a league gets - so it is what proves the page itself rendered rather than which
    /// version of it appeared. The id is deliberately on both branches in the markup.
    /// </summary>
    internal ILocator Container => page.GetByTestId(TestIds.Dashboard);

    /// <summary>
    /// The avatar menu, which the navigation only renders inside <c>AuthorizeView.Authorized</c>. Reaching
    /// the URL proves routing; this proves the client actually holds a valid token and read its claims.
    /// </summary>
    internal ILocator AccountMenuButton => page.GetByTestId(TestIds.NavAccountMenu);

    /// <summary>
    /// The panel every page shows in place of content when a read behind it fails. Asserted absent, because
    /// a dashboard that renders its frame and eight error boxes is not a working dashboard. One id on the
    /// shared component covers every page, so this works unchanged on any screen.
    /// </summary>
    internal ILocator ErrorMessages => page.GetByTestId(TestIds.ApiError);
}
