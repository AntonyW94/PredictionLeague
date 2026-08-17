using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The sign-in screen at <c>/authentication/login</c>, and the way every other journey will start.
/// </summary>
/// <remarks>
/// Owns the form and nothing else. The consent banner it has to get out of the way first belongs to
/// <see cref="SiteLayout"/>, because it is on every page rather than this one.
///
/// Every element is addressed by its <c>data-test-id</c> - see <see cref="TestIds"/> for why, and
/// <c>TestIdConventionTests</c> for what stops one being referenced that the markup does not carry.
/// </remarks>
internal sealed class LoginPage(IPage page)
{
    private ILocator EmailField => page.GetByTestId(TestIds.LoginEmail);

    private ILocator PasswordField => page.GetByTestId(TestIds.LoginPassword);

    private ILocator SubmitButton => page.GetByTestId(TestIds.LoginSubmit);

    /// <summary>
    /// Signs in and waits for the dashboard. The client navigates to <c>/</c>, which bounces an
    /// authenticated visitor on to <c>/dashboard</c>, so the landing URL rather than the click is what
    /// proves it worked.
    /// </summary>
    internal async Task SignInAsync(string email, string password)
    {
        await GoToAsync();

        await EmailField.FillAsync(email);
        await PasswordField.FillAsync(password);
        await SubmitButton.ClickAsync();

        // A predicate rather than a glob, because the wait has to survive two navigations: to "/" and then
        // on to "/dashboard".
        await page.WaitForURLAsync(
            url => url.EndsWith("/dashboard", StringComparison.Ordinal),
            new PageWaitForURLOptions { Timeout = E2ESettings.NavigationTimeoutMs });
    }

    private async Task GoToAsync()
    {
        await page.GotoAsync("/authentication/login");

        // The navigation timeout rather than the assertion one: GotoAsync returns on `load`, and what this
        // actually waits for afterwards is Blazor WebAssembly downloading and starting its runtime, which is
        // a navigation-sized wait - especially for the first test in a run, which also pays the app's own
        // cold start.
        await EmailField.ShouldBeVisibleAsync(E2ESettings.NavigationTimeoutMs);

        // This is the first arrival in the context, which is the one moment the banner is guaranteed to be
        // there - see the remarks on the method.
        await new SiteLayout(page).DismissConsentBannerAsync();
    }
}
