using Microsoft.Playwright;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Reads like the assertions everywhere else in the solution - <c>await field.ShouldBeVisibleAsync()</c>
/// rather than <c>await Assertions.Expect(field).ToBeVisibleAsync()</c> - while still being Playwright's
/// assertions underneath.
/// </summary>
/// <remarks>
/// Note these wrap <b>Playwright</b>, not AwesomeAssertions, despite the <c>Should</c> naming. That is why
/// this project's move off FluentAssertions touched only the convention tests: nothing about the browser ever
/// went through an assertion library.
/// </remarks>
/// <remarks>
/// <para>
/// <b>Every method here delegates to <c>Assertions.Expect</c>, and that is the whole point.</b> Playwright's
/// assertions are <i>web-first</i>: they re-check the page until the condition holds or the timeout expires.
/// A browser is asynchronous - a Blazor render, an API call and a repaint all land after the click that
/// caused them - so an assertion that samples the page once is a race, and it will be the intermittent
/// failure nobody can reproduce.
/// </para>
/// <para>
/// So the tempting version of "make it read like FluentAssertions" is the one to avoid:
/// </para>
/// <code>
/// // WRONG - reads nicely, samples once, races the browser
/// (await locator.CountAsync()).Should().Be(0);
/// (await locator.IsVisibleAsync()).Should().BeTrue();
///
/// // CORRECT - reads the same, retries until it holds or times out
/// await locator.ShouldNotExistAsync();
/// await locator.ShouldBeVisibleAsync();
/// </code>
/// <para>
/// An assertion library is still the right tool in this project for the convention tests, which sweep source
/// files and reflect over types: there is no page to settle, so there is nothing to retry. The rule is the
/// domain, not the syntax - anything about the browser goes through here.
/// </para>
/// </remarks>
internal static class LocatorAssertionExtensions
{
    /// <summary>
    /// Asserts the element is present and visible, retrying until it is.
    /// </summary>
    /// <param name="timeoutMs">
    /// Overrides the context default. Needed for the first assertion after a navigation, which is really
    /// waiting for Blazor WebAssembly to download and start rather than for an element to appear.
    /// </param>
    internal static Task ShouldBeVisibleAsync(this ILocator locator, float? timeoutMs = null) =>
        Assertions.Expect(locator).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = timeoutMs });

    /// <summary>Asserts the element is either gone or no longer visible, retrying until it is.</summary>
    internal static Task ShouldBeHiddenAsync(this ILocator locator, float? timeoutMs = null) =>
        Assertions.Expect(locator).ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = timeoutMs });

    /// <summary>
    /// Asserts nothing on the page matches, retrying until nothing does.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="ShouldBeHiddenAsync"/> on purpose: hidden means rendered but not shown, this
    /// means not rendered at all. For "no error panel anywhere" the second is what is meant, and it is also
    /// the one that reads correctly when the locator matches several elements.
    /// </remarks>
    internal static Task ShouldNotExistAsync(this ILocator locator, float? timeoutMs = null) =>
        Assertions.Expect(locator).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = timeoutMs });

    /// <summary>
    /// Asserts nothing matches, and if something does, says <b>what it said</b>.
    /// </summary>
    /// <remarks>
    /// For error panels, <see cref="ShouldNotExistAsync"/> reports "expected count 0 but was 3", which tells
    /// you a page is broken without telling you how - and the panels are the one place the application has
    /// already written down what went wrong. This ran the retrying assertion first, so the waiting behaviour
    /// is unchanged, and only reaches for the text once the assertion has genuinely failed.
    ///
    /// It earned itself immediately: a dashboard that had passed for many runs started reporting three
    /// errors, and "three" was not enough to act on without downloading a trace - which a GitHub outage had
    /// made unavailable at the time.
    /// </remarks>
    internal static async Task ShouldReportNoErrorsAsync(this ILocator locator, float? timeoutMs = null)
    {
        try
        {
            await locator.ShouldNotExistAsync(timeoutMs);
        }
        catch (PlaywrightException)
        {
            var messages = await locator.AllInnerTextsAsync();

            var detail = messages.Count == 0
                ? "(the panels disappeared before their text could be read)"
                : string.Join($"{Environment.NewLine}  - ", messages.Select(m => m.ReplaceLineEndings(" ").Trim()));

            throw new PlaywrightException(
                $"The page is showing {messages.Count} error panel(s), so a read behind it failed:"
                + $"{Environment.NewLine}  - {detail}");
        }
    }
}
