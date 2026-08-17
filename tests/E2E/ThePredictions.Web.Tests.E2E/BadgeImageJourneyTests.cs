using AwesomeAssertions;
using ThePredictions.Contracts.Badges;
using ThePredictions.Web.Tests.E2E.Harness;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// The badge icon PNG, which is the cheapest possible proof that SkiaSharp renders on Linux.
/// </summary>
/// <remarks>
/// <para>
/// SkiaSharp is a managed wrapper over a native library, and the base package carries the Windows binary
/// only - so <c>SkiaSharp.NativeAssets.Linux</c> had to be added for this suite's Linux runner. That failure
/// mode is nasty precisely because it is <b>lazy</b>: the native library loads on first use, so the
/// application starts perfectly happily and then throws <c>DllNotFoundException</c> on the first request that
/// draws something. Adding the package without exercising it would have been a change made on faith.
/// </para>
/// <para>
/// This endpoint is the ideal probe for it. It is <c>[AllowAnonymous]</c>, because email clients rendering the
/// round-results digest are not logged in, so the test needs no sign-in and no seeded data at all. And badge
/// glyphs are drawn as pure paths - nothing in the renderer touches <c>SKFont</c>, <c>SKTypeface</c> or
/// <c>DrawText</c> - so it needs no system fonts either.
/// </para>
/// <para>
/// The share card is the opposite on both counts: it draws text in seventeen places, so it additionally needs
/// fontconfig and fonts, and its correctness is visual rather than assertable. It stays out of scope, and the
/// plan says so.
/// </para>
/// </remarks>
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public class BadgeImageJourneyTests(StackFixture stack) : E2ETestBase(stack)
{
    [Fact]
    public async Task BadgeIcon_ShouldRenderAsAPng_ProvingSkiaSharpWorksOnLinux()
    {
        await using var session = await StartSessionAsync();

        var response = await session.Page.APIRequest.GetAsync($"/api/badges/{BadgeKeys.Marksman1}.png");

        // A native library failure surfaces as a 500 from the error-handling middleware, so the status alone
        // is the assertion that matters. It is stated with the reason attached because "expected 200 but got
        // 500" on its own would send the next reader looking at the badge query rather than at a missing .so.
        response.Status.Should().Be(200,
            $"GET /api/badges/{BadgeKeys.Marksman1}.png rasterises an SVG through SkiaSharp. A 500 here on "
            + "Linux almost certainly means the native libSkiaSharp could not be loaded - check that "
            + "SkiaSharp.NativeAssets.Linux is still referenced by ThePredictions.Infrastructure, and that "
            + "its version still matches the managed SkiaSharp package exactly.");

        response.Headers.Should().ContainKey("content-type")
            .WhoseValue.Should().Contain("image/png");

        // Renderers can return an empty buffer without throwing, which would pass a status check and be a
        // broken image in an email. A real PNG is comfortably over a few hundred bytes.
        var body = await response.BodyAsync();

        body.Length.Should().BeGreaterThan(500,
            "a rendered badge is a real PNG, not an empty or truncated buffer - an image that decodes to "
            + "nothing would still return 200 and would still be broken in the digest email.");
    }
}
