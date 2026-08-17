using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// The <c>data-test-id</c> contract has one end in C# and the other in a <c>.razor</c> file, so half of it
/// is a string the compiler cannot see. Delete an attribute from the markup and nothing fails to build - the
/// journey just times out later, somewhere else, looking like a broken page rather than a moved id.
///
/// These tests are that missing half. They read the markup rather than the running site, so they need no
/// database, no browser and no Docker, and they fail in seconds.
/// </summary>
[Trait(E2ETrait.Name, E2ETrait.Value)]
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public partial class TestIdConventionTests
{
    [GeneratedRegex(@"data-test-id\s*=\s*""(?<id>[^""]+)""", RegexOptions.Compiled)]
    private static partial Regex TestIdAttribute();

    /// <summary>The mis-spelling that would be invisible: Playwright's own default, and not what is configured.</summary>
    [GeneratedRegex(@"data-testid\s*=", RegexOptions.Compiled)]
    private static partial Regex WrongAttributeSpelling();

    [Fact]
    public void EveryReferencedTestId_ShouldExistInTheMarkup()
    {
        var inMarkup = IdsInMarkup();

        var missing = TestIds.All
            .Where(id => !inMarkup.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        missing.Should().BeEmpty(
            $"the suite addresses elements only by {TestIds.Attribute}, so an id it names that no markup "
            + "carries can never be found. Either the attribute was dropped from the component or the "
            + "constant is a typo - and the failure without this test is a timeout on an unrelated-looking "
            + "assertion.");
    }

    /// <summary>
    /// Keeps <see cref="TestIds.All"/> honest. It is what the test above sweeps, so a constant missing from
    /// it is a constant nobody checks - the list would silently become a partial record of the ids in use.
    /// </summary>
    [Fact]
    public void TheListOfIds_ShouldContainEveryConstant()
    {
        var declared = typeof(TestIds)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, FieldType.Name: nameof(String) })
            .Where(field => field.Name != nameof(TestIds.Attribute))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        declared.Should().BeSubsetOf(TestIds.All,
            $"add new ids to {nameof(TestIds)}.{nameof(TestIds.All)} as well as declaring them, or the "
            + "convention above will not check them.");
    }

    /// <summary>
    /// Playwright's built-in default is <c>data-testid</c>, and the fixture reconfigures it to
    /// <c>data-test-id</c>. Markup written with the default spelling would therefore look completely correct
    /// and be entirely invisible to the suite, which is the sort of thing that costs an afternoon.
    /// </summary>
    [Fact]
    public void NoMarkup_ShouldUseThePlaywrightDefaultSpelling()
    {
        var offenders = WebClientSource.MarkupFiles()
            .Where(file => WrongAttributeSpelling().IsMatch(file.Text))
            .Select(file => file.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            $"this repository uses {TestIds.Attribute}, with a hyphen, and the fixture points Playwright at "
            + "it. `data-testid` is Playwright's default rather than ours, so an element carrying it would "
            + "look annotated and be unfindable.");
    }

    private static HashSet<string> IdsInMarkup() =>
        WebClientSource.MarkupFiles()
            .SelectMany(file => TestIdAttribute().Matches(file.Text))
            .Select(match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);
}
