using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ThePredictions.Conventions.Tests.Unit;

/// <summary>
/// A write that stores many rows does it in one statement.
///
/// Dapper executes a command once per element when its parameter is an <c>IEnumerable</c>, sequentially, on the same
/// connection. It reads like a batch and is not one: a single scoring tick was making roughly 130 round trips to a
/// database on another machine, each holding the transaction's write locks open a little longer, which is what an
/// unrelated dashboard read was measured waiting 615ms behind (see
/// <c>docs/decisions/0020-set-based-writes.md</c>). Every such write now passes its rows as one JSON parameter and
/// reads them back with <c>OPENJSON</c>.
///
/// These tests guard the two ways that goes wrong quietly.
/// </summary>
public class SetBasedWriteConventionTests
{
    private const string AdapterRoot = "src/ThePredictions.Persistence.SqlServer/";

    /// <summary>
    /// <c>parameters: xs.Select(x => new { ... })</c> is the row-by-row form, and it is indistinguishable at a glance
    /// from the set-based one - the only difference is whether Dapper is handed one object or a sequence of them.
    /// Four of the writes converted here were written exactly like this.
    /// </summary>
    private static readonly Regex InlineProjectionAsParameters = new(
        @"CommandDefinition\(\s*(?:commandText:\s*)?[A-Za-z0-9_.]+\s*,\s*(?:parameters:\s*)?[A-Za-z0-9_.]+\s*\.Select\(",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void NoCommand_ShouldTakeASequenceOfRowsAsItsParameters()
    {
        var offenders = AdapterSourceFiles()
            .Where(file => InlineProjectionAsParameters.IsMatch(file.Text))
            .Select(file => file.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "Dapper runs the statement once per element when its parameter is a sequence, so this is N round trips "
            + "rather than one, each extending the transaction that holds the write locks. Pass the rows as one "
            + "parameter with JsonRows.From(...) and read them back with OPENJSON - see "
            + "docs/guides/database.md#set-based-writes.");
    }

    /// <summary>
    /// The positive control for the sweep above: the pattern it looks for has to be one this codebase would actually
    /// produce, or the sweep passes because it has stopped matching anything rather than because the writes are set
    /// based.
    /// </summary>
    [Fact]
    public void TheInlineProjectionDetector_ShouldStillRecogniseTheFormItBans()
    {
        const string offendingSource =
            "var command = new CommandDefinition(sql, matches.Select(m => new { m.Id }), transaction: Transaction);";

        InlineProjectionAsParameters.IsMatch(offendingSource).Should().BeTrue(
            "a detector that no longer recognises the row-by-row form would let it back in silently.");
    }

    /// <summary>
    /// Every JSON path in the adapter, with the column it feeds.
    /// </summary>
    private static readonly Regex JsonPath = new(
        @"\[(?<column>\w+)\]\s+[\w()\s,]+?'(?<mode>strict\s+)?\$\.(?<property>\w+)'",
        RegexOptions.Compiled);

    /// <summary>
    /// <c>OPENJSON</c> is lax by default: a path naming a property the JSON does not carry yields NULL rather than an
    /// error. So a single typo in a <c>WITH</c> clause silently writes NULL over a column - no exception, no warning,
    /// and for a nullable column no failure of any kind. Verified against the live server: <c>strict</c> raises
    /// "Property cannot be found on the specified JSON path" instead, and still accepts a property that is present
    /// and null, which is what the serialiser emits for a null value.
    /// </summary>
    [Fact]
    public void EveryJsonPath_ShouldBeStrict()
    {
        var offenders = JsonPaths()
            .Where(path => string.IsNullOrEmpty(path.Match.Groups["mode"].Value))
            .Select(path => $"{path.RelativePath}: [{path.Match.Groups["column"].Value}]")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a lax path that names a property the row objects do not carry writes NULL rather than failing. Write "
            + "'strict $.Column' so a mismatch is an error.");
    }

    /// <summary>
    /// The rows are anonymous objects built next to the statement, so a column and the property feeding it are always
    /// meant to be the same name. Requiring that spelled out is what makes <c>strict</c> checkable by eye: the pair
    /// either reads as one name twice or it is wrong.
    /// </summary>
    [Fact]
    public void EveryJsonPath_ShouldNameTheColumnItFeeds()
    {
        var offenders = JsonPaths()
            .Where(path => path.Match.Groups["column"].Value != path.Match.Groups["property"].Value)
            .Select(path =>
                $"{path.RelativePath}: [{path.Match.Groups["column"].Value}] <- $.{path.Match.Groups["property"].Value}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a path pointing at a differently named property is either a typo or a rename that stopped half way. Name "
            + "the JSON property after the column.");
    }

    /// <summary>
    /// A declared type with a width in it, which is the one thing a <c>WITH</c> clause must never state.
    /// </summary>
    private static readonly Regex WidthBearingType = new(
        @"\[(?<column>\w+)\]\s+(?<type>(?:n?var)?char\(\s*\d+\s*\)|n?text|decimal\(\s*\d+\s*,\s*\d+\s*\)|numeric\(\s*\d+\s*,\s*\d+\s*\))\s+'strict ",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// A <c>WITH</c> clause declares a type in order to read the JSON, and that declaration is a **cast** - so one
    /// narrower than the column it feeds silently truncates. Verified against the live server:
    /// <c>nvarchar(20)</c> reading a 28-character value stored 20 characters and raised nothing, and
    /// <c>decimal(18,0)</c> reading 12.349 stored 12.00. <c>strict</c> does not help; it only guards whether the
    /// property is <i>there</i>.
    ///
    /// Two of the first 70 declarations written here were narrower than their column, which is the measured error
    /// rate for copying a width by hand. So no width is copied: the types are deliberately wider than any column
    /// they feed, which makes the destination column the only place a width is stated and turns an overflow into
    /// SQL Server's own "String or binary data would be truncated" at the insert. A wide <c>decimal</c> is rounded
    /// to the column's scale on the way in, which is the right answer rather than a lost one.
    /// </summary>
    [Fact]
    public void NoJsonColumn_ShouldDeclareAWidth()
    {
        var offenders = AdapterSourceFiles()
            .SelectMany(file => WidthBearingType.Matches(file.Text).Select(match => (file.RelativePath, Match: match)))
            .Where(entry => IsACopiedWidth(entry.Match.Groups["type"].Value))
            .Select(entry =>
                $"{entry.RelativePath}: [{entry.Match.Groups["column"].Value}] {entry.Match.Groups["type"].Value}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a declared width narrower than the column silently truncates, and a width is only ever right by "
            + "accident of it being copied correctly. Use nvarchar(4000) for text and decimal(38, 10) for money, "
            + "and let the column decide - see docs/guides/database.md#set-based-writes.");
    }

    /// <summary>
    /// The positive control for the sweep above.
    /// </summary>
    [Fact]
    public void TheWidthDetector_ShouldStillRecogniseADeclaredWidth()
    {
        Offends("[Status] nvarchar(50) 'strict $.Status'").Should().BeTrue();
        Offends("[Amount] decimal(18, 2) 'strict $.Amount'").Should().BeTrue();
        Offends("[Code] char(3) 'strict $.Code'").Should().BeTrue();

        Offends("[Status] nvarchar(4000) 'strict $.Status'").Should().BeFalse(
            "4000 is the deliberate wide default rather than a copied column width.");
        Offends("[Amount] decimal(38, 10) 'strict $.Amount'").Should().BeFalse();
        Offends("[RoundId] int 'strict $.RoundId'").Should().BeFalse(
            "int, bit and datetime2 have no width to get wrong.");
        return;

        static bool Offends(string declaration)
        {
            var match = WidthBearingType.Match(declaration);
            return match.Success && IsACopiedWidth(match.Groups["type"].Value);
        }
    }

    /// <summary>
    /// The two wide forms every text and money column is read through. Anything else with a width in it is a
    /// column's own width, copied - which is the thing that cannot be checked and was twice wrong.
    /// </summary>
    private static bool IsACopiedWidth(string declaredType) =>
        !declaredType.Replace(" ", string.Empty).Equals("nvarchar(4000)", StringComparison.OrdinalIgnoreCase)
        && !declaredType.Replace(" ", string.Empty).Equals("decimal(38,10)", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The positive control for both path sweeps. An empty result set from a regex that has stopped matching would
    /// make them pass for ever without reading anything.
    /// </summary>
    [Fact]
    public void TheJsonPathDetector_ShouldStillFindThePathsInTheAdapter()
    {
        JsonPaths().Should().NotBeEmpty(
            "the adapter's set-based writes declare their columns with JSON paths, so finding none means this test "
            + "class has stopped looking rather than that the writes have changed shape.");
    }

    private static List<(string RelativePath, Match Match)> JsonPaths() =>
        AdapterSourceFiles()
            .SelectMany(file => JsonPath.Matches(file.Text).Select(match => (file.RelativePath, Match: match)))
            .ToList();

    private static List<(string RelativePath, string Text)> AdapterSourceFiles() =>
        ProductionAssemblies.SourceFiles(".cs")
            .Where(file => file.RelativePath.StartsWith(AdapterRoot, StringComparison.Ordinal))
            .ToList();
}
