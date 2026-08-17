using System.Text.RegularExpressions;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Reads the Blazor client's own source, for the conventions that can only be checked against markup rather
/// than against a running site: which <c>data-test-id</c> attributes exist, and which routes the application
/// actually has.
/// </summary>
internal static partial class WebClientSource
{
    [GeneratedRegex(@"^@page\s+""(?<route>[^""]+)""", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex PageDirective();

    /// <summary>
    /// Every hand-written markup file under <c>src/</c>. Razor and HTML both, because the Web host's
    /// <c>index.html</c> is as legitimate a place for a test id as a component is.
    /// </summary>
    internal static IEnumerable<(string RelativePath, string Text)> MarkupFiles()
    {
        var sourceRoot = Path.Combine(E2ESettings.RepositoryRoot, "src");

        if (!Directory.Exists(sourceRoot))
            throw new InvalidOperationException($"Expected to find the source tree at '{sourceRoot}'.");

        foreach (var extension in new[] { ".razor", ".html" })
        {
            foreach (var path in Directory.EnumerateFiles(sourceRoot, $"*{extension}", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(E2ESettings.RepositoryRoot, path).Replace('\\', '/');

                if (relative.Contains("/obj/") || relative.Contains("/bin/"))
                    continue;

                // utf-8 with BOM detection: several components are authored with one, and a byte-order mark
                // sitting in front of `@page` defeats a line-anchored match. Getting this wrong under-counts
                // the routes silently, which is exactly what a completeness check must not do.
                yield return (relative, File.ReadAllText(path));
            }
        }
    }

    /// <summary>
    /// Every route the application serves, read from the <c>@page</c> directives. A component may declare
    /// more than one, so this is a set of routes rather than of files.
    /// </summary>
    internal static IReadOnlyCollection<string> Routes() =>
        MarkupFiles()
            .Where(file => file.RelativePath.Contains("/ThePredictions.Web.Client/", StringComparison.Ordinal))
            .SelectMany(file => PageDirective().Matches(file.Text))
            .Select(match => match.Groups["route"].Value)
            .ToHashSet(StringComparer.Ordinal);
}
