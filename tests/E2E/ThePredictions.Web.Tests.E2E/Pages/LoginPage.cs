using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The sign-in screen at <c>/authentication/login</c>, and the way every other journey starts.
/// </summary>
internal sealed class LoginPage(IPage page)
{
    /// <summary>
    /// Shorter than the suite's other waits on purpose: by the time this runs the app has already rendered
    /// the login form, so the banner is not waiting on a download.
    /// </summary>
    private const float BannerTimeoutMs = 15_000;

    private ILocator EmailField => page.Locator("#email");

    private ILocator PasswordField => page.Locator("#password");

    private ILocator SubmitButton => page.Locator("button[type='submit']");

    /// <summary>
    /// Signs in and waits for the dashboard. Login navigates to <c>/</c>, which bounces an authenticated
    /// visitor on to <c>/dashboard</c>, so the landing URL rather than the click is what proves it worked.
    /// </summary>
    internal async Task SignInAsync(string email)
    {
        await GoToAsync();

        await EmailField.FillAsync(email);
        await PasswordField.FillAsync(RequiredPassword());
        await SubmitButton.ClickAsync();

        // A predicate rather than a glob, because the wait has to survive two navigations: the client goes
        // to "/", and the home page then redirects an authenticated visitor to "/dashboard".
        await page.WaitForURLAsync(
            url => url.EndsWith("/dashboard", StringComparison.Ordinal),
            new PageWaitForURLOptions { Timeout = E2ESettings.NavigationTimeoutMs });
    }

    private async Task GoToAsync()
    {
        await page.GotoAsync("/authentication/login");

        // The navigation timeout rather than the assertion one: GotoAsync returns on `load`, and what this
        // actually waits for afterwards is Blazor WebAssembly downloading and starting its runtime, which is
        // a navigation-sized wait rather than an assertion-sized one - especially for the first test in a run.
        await Assertions.Expect(EmailField).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = E2ESettings.NavigationTimeoutMs });

        await DismissCookieBannerAsync();
    }

    /// <summary>
    /// Answers the consent banner before touching the form. It is fixed to the foot of the page, where it can
    /// sit over a control and swallow a click, so it is dealt with rather than ignored. Answered through the
    /// UI rather than by seeding local storage on purpose: seeding would tie the suite to the stored consent
    /// record's JSON shape, which is an implementation detail it should not know.
    /// </summary>
    /// <remarks>
    /// Waited for unconditionally, because its appearance is deterministic: every test gets a fresh browser
    /// context, so local storage is empty, <c>ConsentBannerService.HasResponded</c> starts false, and the
    /// banner renders on the layout's first pass. Probing instead of waiting is the tempting alternative and
    /// is wrong - <c>IsVisibleAsync</c> does not wait (its <c>Timeout</c> is deprecated for that reason), so
    /// it can answer "no" a moment before the banner slides in. If the banner is ever retired, this line
    /// fails loudly in one file, which is the right amount of maintenance for a real change in behaviour.
    /// </remarks>
    private async Task DismissCookieBannerAsync()
    {
        var rejectButton = page.Locator(".cookie-consent__btn--secondary");

        await rejectButton.WaitForAsync(new LocatorWaitForOptions { Timeout = BannerTimeoutMs });
        await rejectButton.ClickAsync();

        await Assertions.Expect(page.Locator(".cookie-consent")).ToBeHiddenAsync();
    }

    private static string RequiredPassword() =>
        E2ESettings.TestAccountPassword
        ?? throw new InvalidOperationException(E2ESettings.NotConfiguredReason);
}
