using Microsoft.Playwright;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// One test's browser context and page, which writes a Playwright trace on the way out.
/// </summary>
/// <remarks>
/// The trace is written for every test, pass or fail, rather than only on failure. Knowing the outcome from
/// inside the test would mean reading runner state, and a trace of a passing run costs a few hundred
/// kilobytes and nothing else - the workflow uploads the folder only when the job fails, so the choice of
/// what to keep is made where the outcome is actually known.
/// </remarks>
internal sealed class BrowserSession(IBrowserContext context, IPage page, string testName) : IAsyncDisposable
{
    internal IPage Page { get; } = page;

    public async ValueTask DisposeAsync()
    {
        Directory.CreateDirectory(E2ESettings.ArtifactsDirectory);

        await context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = Path.Combine(E2ESettings.ArtifactsDirectory, $"{Sanitise(testName)}.zip")
        });

        await context.DisposeAsync();
    }

    private static string Sanitise(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
