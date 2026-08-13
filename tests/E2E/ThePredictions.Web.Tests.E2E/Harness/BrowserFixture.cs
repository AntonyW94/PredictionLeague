using Microsoft.Playwright;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Launches one Chromium per run and hands each test its own isolated context. Sharing the *browser* is
/// what keeps the suite quick (a launch costs seconds); isolating the *context* is what keeps it honest,
/// because a context carries the cookies and local storage that hold the signed-in session, so two tests
/// sharing one would share a login.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>
    /// Nothing is launched when the suite is unconfigured. The tests skip in that case, and a fixture that
    /// insisted on a browser first would turn every skip into an error before the test could say why.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        if (!E2ESettings.IsConfigured)
            return;

        _playwright = await Playwright.CreateAsync();

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !E2ESettings.RunHeaded,
            SlowMo = E2ESettings.SlowMoMs
        });
    }

    internal async Task<BrowserSession> NewSessionAsync(string testName)
    {
        if (_browser is null)
            throw new InvalidOperationException("The browser was never launched. " + E2ESettings.NotConfiguredReason);

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = E2ESettings.BaseUrl,
            // A desktop viewport: below 992px the dashboard collapses into its mobile tab strip and hides
            // whole tiles behind buttons, which would make a structural assertion depend on the layout
            // breakpoint rather than on the page having rendered.
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            IgnoreHTTPSErrors = false
        });

        context.SetDefaultNavigationTimeout(E2ESettings.NavigationTimeoutMs);
        context.SetDefaultTimeout(E2ESettings.AssertionTimeoutMs);

        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Title = testName,
            Screenshots = true,
            Snapshots = true,
            Sources = false
        });

        var page = await context.NewPageAsync();

        return new BrowserSession(context, page, testName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();
    }
}
