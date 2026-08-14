using Microsoft.Playwright;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// The whole stack, built once per run and destroyed with it: a SQL Server container, production's schema
/// applied by the committed migrations, a seeded user, the published application running against it, and a
/// Chromium to drive it.
/// </summary>
/// <remarks>
/// Order matters. The migrations have to precede the application, because its <c>DatabaseInitialiser</c>
/// hosted service writes the Identity roles at startup and needs somewhere to write them. Everything is
/// torn down in reverse, and the browser goes first so a page cannot be mid-request when the site stops.
/// </remarks>
public sealed class StackFixture : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private readonly WebApplicationProcess _application = new();

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        await _application.StartAsync(_database.ConnectionString);

        _playwright = await Playwright.CreateAsync();

        // Playwright's GetByTestId looks for `data-testid` out of the box. The markup uses `data-test-id`,
        // which is the more readable spelling and the one this repository settled on, so the engine is
        // pointed at it here - once, rather than every page object hand-writing an attribute selector.
        _playwright.Selectors.SetTestIdAttribute(TestIds.Attribute);

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !E2ESettings.RunHeaded,
            SlowMo = E2ESettings.SlowMoMs
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();

        await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    /// A fresh browser context per test. Isolated because a context carries the cookies and local storage
    /// holding a signed-in session, so two tests sharing one would share a login.
    /// </summary>
    internal async Task<BrowserSession> NewSessionAsync(string testName)
    {
        if (_browser is null)
            throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run.");

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = E2ESettings.BaseUrl,
            // A desktop viewport: below 992px the dashboard collapses into a mobile tab strip and hides
            // whole tiles behind buttons, which would make a structural assertion depend on the layout
            // breakpoint rather than on the page having rendered.
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
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
}
