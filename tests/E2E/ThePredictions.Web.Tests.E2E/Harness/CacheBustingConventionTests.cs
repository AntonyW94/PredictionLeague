using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Publish rewrites the stylesheet and script tags in <c>index.html</c> to carry a timestamp, so a returning
/// browser fetches the new file rather than its cached copy. It does that with literal find-and-replace: the
/// <c>Find</c> attribute of each <c>ReplaceInFile</c> in <c>ThePredictions.Web.csproj</c> has to appear in
/// <c>index.html</c> exactly, character for character.
/// </summary>
/// <remarks>
/// <para>
/// Nothing checked that, and it stopped being true. Someone added a hand-maintained <c>?v=2</c> to the
/// <c>interop.js</c> tag - a reasonable-looking thing to do, with a comment saying to bump it when the file
/// changed - which made the target's <c>Find</c> stop matching. The build carried on reporting "Cache busting
/// complete", the other three files kept being stamped, and <c>interop.js</c> alone was frozen at
/// <c>?v=2</c> for every deploy after that.
/// </para>
/// <para>
/// It surfaced as a bug with no plausible connection to caching: the admin delete dialog failed for a real
/// user on dev while passing in this suite, because a browser context here is always new and has nothing
/// cached, and only a <b>returning</b> browser holds the stale file. That is what makes this worth a test
/// rather than a comment - the failure mode is invisible to every check that starts from a clean state.
/// </para>
/// </remarks>
[Trait(E2ETrait.Name, E2ETrait.Value)]
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public class CacheBustingConventionTests
{
    private static readonly string WebProjectPath = Path.Combine(
        E2ESettings.RepositoryRoot, "src", "ThePredictions.Web", "ThePredictions.Web.csproj");

    private static readonly string IndexHtmlPath = Path.Combine(
        E2ESettings.RepositoryRoot, "src", "ThePredictions.Web.Client", "wwwroot", "index.html");

    [Fact]
    public void EveryCacheBustingPattern_ShouldMatchTheMarkupItRewrites()
    {
        // Arrange
        var indexHtml = ReadIndexHtml();

        // Act
        var unmatched = CacheBustingPatterns()
            .Where(pattern => !indexHtml.Contains(pattern, StringComparison.Ordinal))
            .OrderBy(pattern => pattern, StringComparer.Ordinal)
            .ToList();

        // Assert
        unmatched.Should().BeEmpty(
            "publish rewrites these by literal match, so a pattern that index.html does not contain is a file "
            + "that silently never gets a cache-busting stamp. The build still says it succeeded, and the "
            + "consequence only appears for a returning browser - which no test starting from a clean profile "
            + "will ever be.");
    }

    /// <summary>
    /// The other direction: a locally-hosted asset in the markup that no pattern rewrites.
    /// </summary>
    /// <remarks>
    /// Third-party files under <c>lib/</c> are excluded, and Blazor's own <c>_framework/</c> scripts too -
    /// those are versioned by the framework's integrity manifest rather than by this target.
    /// </remarks>
    [Fact]
    public void EveryLocalAsset_ShouldBeCoveredByACacheBustingPattern()
    {
        // Arrange
        var patterns = CacheBustingPatterns().ToList();

        // Act
        var unstamped = LocalAssetReferences(ReadIndexHtml())
            .Where(reference => !patterns.Any(pattern => pattern.Contains(reference, StringComparison.Ordinal)))
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();

        // Assert
        unstamped.Should().BeEmpty(
            "a stylesheet or script we host ourselves that publish does not stamp will be served from cache "
            + "after it changes. Add a ReplaceInFile line for it to BundleCssAndAddCacheBusting in "
            + "ThePredictions.Web.csproj.");
    }

    /// <summary>
    /// The <c>Find</c> value of every <c>ReplaceInFile</c> that rewrites <c>index.html</c>, XML-decoded - so
    /// <c>&amp;quot;</c> in the project file is read here as the quote character it stands for, which is what
    /// the target itself searches for.
    /// </summary>
    private static IEnumerable<string> CacheBustingPatterns() =>
        XDocument.Load(WebProjectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "ReplaceInFile")
            .Where(element => (string?)element.Attribute("FilePath") == "$(IndexHtmlPath)")
            .Select(element => (string?)element.Attribute("Find"))
            .Where(find => !string.IsNullOrWhiteSpace(find))
            .Select(find => find!)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// Every <c>href</c> and <c>src</c> in the markup pointing at a file this repository ships itself.
    /// </summary>
    private static IEnumerable<string> LocalAssetReferences(string indexHtml) =>
        System.Text.RegularExpressions.Regex
            .Matches(indexHtml, @"(?:href|src)\s*=\s*""(?<path>(?:css|js)/[^""]+)""")
            .Select(match => match.Groups["path"].Value)
            .Where(path => !path.StartsWith("lib/", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);

    private static string ReadIndexHtml()
    {
        File.Exists(IndexHtmlPath).Should().BeTrue($"the client's index.html should be at {IndexHtmlPath}.");

        return File.ReadAllText(IndexHtmlPath);
    }
}
