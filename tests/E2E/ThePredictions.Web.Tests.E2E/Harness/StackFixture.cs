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
    private readonly WebApplicationProcess _application = new();

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private int _sessionNumber;

    /// <summary>
    /// A fresh, private-range address per session, so no two tests share a rate-limit partition. Documentation
    /// range (192.0.2.0/24, RFC 5737) so it cannot be confused with a real client if it ever turns up in a log.
    /// </summary>
    private string NextClientAddress()
    {
        var n = Interlocked.Increment(ref _sessionNumber);

        return $"192.0.2.{n % 254 + 1}";
    }

    /// <summary>
    /// The database behind the running application, so a test class can arrange its own season and league in
    /// <c>InitializeAsync</c> - see <see cref="TestDatabase.SeedLeagueAsync"/> for why arrangement is per
    /// class rather than shared.
    /// </summary>
    internal TestDatabase Database { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await Database.StartAsync();

        await _application.StartAsync(Database.ConnectionString);

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
        await Database.DisposeAsync();
    }

    /// <summary>
    /// A fresh browser context per test. Isolated because a context carries the cookies and local storage
    /// holding a signed-in session, so two tests sharing one would share a login.
    /// </summary>
    internal async Task<BrowserSession> NewSessionAsync(string testName)
    {
        if (_browser is null)
            throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run.");

        // A distinct client address per test, which gives each its own rate-limit partition.
        //
        // Without it the suite defeats itself: the API's global limiter allows 100 requests per minute per
        // client address, a dashboard load fires eight parallel API calls, and four journeys inside half a
        // minute all arrive from one browser on one address. The overflow comes back as 429, which is not an
        // exception, so nothing appears in the application log - the page just renders error panels for
        // whichever reads happened to lose the race. That looked exactly like an application bug.
        //
        // This works because GetClientIpAddress reads X-Forwarded-For straight off the request. Note that is
        // ALSO true for real traffic, which means the limiter can be bypassed by anyone willing to vary the
        // header - worth raising separately, since it is what protects the sign-in endpoints.
        var clientAddress = NextClientAddress();

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Forwarded-For"] = clientAddress },
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
