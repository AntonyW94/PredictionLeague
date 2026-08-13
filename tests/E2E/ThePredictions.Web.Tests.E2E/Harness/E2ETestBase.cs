using System.Runtime.CompilerServices;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Base for every browser test in this assembly.
/// </summary>
/// <remarks>
/// The session is started from inside the test body rather than from <c>IAsyncLifetime.InitializeAsync</c>,
/// so that the "no password configured" skip is raised before anything tries to open a browser. Setting up
/// first and checking afterwards would report an unconfigured machine as a Playwright error rather than as
/// the skip it is.
/// </remarks>
[Collection(BrowserCollection.Name)]
[Trait(E2ETrait.Name, E2ETrait.Value)]
public abstract class E2ETestBase(BrowserFixture fixture)
{
    /// <summary>
    /// Opens an isolated browser context for the calling test, or skips the test when the suite has no
    /// credentials to sign in with.
    /// </summary>
    internal async Task<BrowserSession> StartSessionAsync([CallerMemberName] string testName = "")
    {
        Assert.SkipUnless(E2ESettings.IsConfigured, E2ESettings.NotConfiguredReason);

        // Qualified by the class, because the trace file is named after this and two actors can reasonably
        // want the same test name.
        return await fixture.NewSessionAsync($"{GetType().Name}.{testName}");
    }
}
