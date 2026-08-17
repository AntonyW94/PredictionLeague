using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// Everything <c>Components/Layout/</c> renders around whatever page you are on: the navigation bar, the
/// consent banner, and the error panel any page shows in place of content when a read behind it fails.
/// </summary>
/// <remarks>
/// These belong here rather than on a page object because they are not on a page - they are on <i>every</i>
/// page. Filing the consent banner under the login screen made it look like a login concern, which it is
/// not: it is fixed to the foot of the layout, so the first journey to start anywhere other than login would
/// have met an undismissed overlay and had a click silently swallowed. The account menu and the error panel
/// were misfiled on the dashboard for the same reason.
///
/// The practical payoff is that "no error panel anywhere" is one assertion that works unchanged on any
/// screen, and a new page object inherits none of this by accident.
/// </remarks>
internal sealed class SiteLayout(IPage page)
{
    /// <summary>
    /// The avatar menu, which the navigation only renders inside <c>AuthorizeView.Authorized</c>. Reaching a
    /// URL proves routing; this proves the client actually holds a valid token and read its claims.
    /// </summary>
    internal ILocator AccountMenuButton => page.GetByTestId(TestIds.NavAccountMenu);

    /// <summary>
    /// Error panels anywhere on the current page. One <c>data-test-id</c> on the shared <c>ApiError</c>
    /// component covers the whole application, so a page that renders its frame and eight failed reads is
    /// caught rather than mistaken for a working one.
    /// </summary>
    internal ILocator ErrorMessages => page.GetByTestId(TestIds.ApiError);

    private ILocator ConsentBanner => page.GetByTestId(TestIds.CookieConsent);

    private ILocator ConsentRejectButton => page.GetByTestId(TestIds.CookieConsentReject);

    /// <summary>
    /// Answers the consent banner, declining everything non-essential.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Call this once, on the first arrival in a browser context.</b> It waits for the banner rather than
    /// probing for it, which is correct exactly once: a fresh context has empty local storage, so
    /// <c>ConsentBannerService.HasResponded</c> starts false and the banner renders on the layout's first
    /// pass. Calling it a second time in the same context would wait for a banner that has already been
    /// answered and will not return.
    /// </para>
    /// <para>
    /// Probing with <c>IsVisibleAsync</c> instead is the tempting alternative and is wrong - it does not wait
    /// (its <c>Timeout</c> is deprecated for that reason), so it can answer "no" a moment before the banner
    /// slides in and leave it free to swallow a later click.
    /// </para>
    /// <para>
    /// Seeding the consent record into local storage up front would avoid the banner entirely and cost
    /// nothing per test. Rejected deliberately: it would tie the suite to the stored record's JSON shape,
    /// including how its enum serialises, which is an implementation detail no test should know - and it
    /// would mean nothing ever exercises the banner at all.
    /// </para>
    /// </remarks>
    internal async Task DismissConsentBannerAsync()
    {
        await ConsentRejectButton.WaitForAsync(new LocatorWaitForOptions { Timeout = ConsentBannerTimeoutMs });
        await ConsentRejectButton.ClickAsync();

        await ConsentBanner.ShouldBeHiddenAsync();
    }

    /// <summary>
    /// Shorter than the suite's other waits on purpose: this is called after the page under test has already
    /// rendered, so the banner is not waiting on the WebAssembly download.
    /// </summary>
    private const float ConsentBannerTimeoutMs = 15_000;
}
