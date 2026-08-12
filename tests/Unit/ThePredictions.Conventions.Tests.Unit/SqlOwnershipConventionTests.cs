using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ThePredictions.Conventions.Tests.Unit;

/// <summary>
/// SQL belongs to the persistence adapter, and nowhere else.
///
/// <c>ThePredictions.Application</c> once held 331 <c>SELECT</c>s across 53 files, with the business rules that decide what a
/// player sees written as <c>CASE</c> expressions, <c>ISNULL</c> sentinels and nested <c>NOT EXISTS</c> blocks inside them -
/// untestable by anything that did not have a database. The persistence split moved every one behind a port owned by
/// Application and implemented in <c>ThePredictions.Persistence.SqlServer</c>, with the rules rewritten in C# and unit tested
/// (see <c>docs/todo/architecture/persistence-split</c>).
///
/// These tests are what stops that coming back. The plan can be re-read; a failing build cannot be skimmed past.
/// </summary>
public class SqlOwnershipConventionTests
{
    private const string ApplicationRoot = "src/ThePredictions.Application/";
    private const string AdapterRoot = "src/ThePredictions.Persistence.SqlServer/";

    /// <summary>
    /// Written in upper case on purpose. Every one is how this codebase writes SQL and none is how it writes C#: the SQL style
    /// guide puts each keyword on its own line in capitals, so a keyword in this form is a statement rather than a coincidence.
    /// <c>.Where(</c>, <c>OrderBy(</c> and the rest of LINQ are untouched by it.
    /// </summary>
    private static readonly Regex[] SqlSignatures =
    [
        new(@"\bSELECT\b", RegexOptions.Compiled),
        new(@"\bINSERT\s+INTO\b", RegexOptions.Compiled),
        new(@"\bDELETE\s+FROM\b", RegexOptions.Compiled),
        new(@"\bMERGE\b", RegexOptions.Compiled),
        new(@"\bFROM\s+\[", RegexOptions.Compiled),
        new(@"\bUPDATE\s+\[", RegexOptions.Compiled),
        new(@"\b(INNER|LEFT|RIGHT|CROSS)\s+JOIN\b", RegexOptions.Compiled),
        new(@"\b(GROUP|ORDER)\s+BY\b", RegexOptions.Compiled),
        new(@"\bWHERE\s", RegexOptions.Compiled)
    ];

    [Fact]
    public void NoApplicationFile_ShouldContainSql()
    {
        var offenders = SqlBearingFiles(ApplicationRoot);

        offenders.Should().BeEmpty(
            "SQL in Application is what the persistence split removed. A read belongs behind an interface in Application and "
            + "a class in ThePredictions.Persistence.SqlServer, with a conformance test for the read and unit tests for the "
            + "rules the handler applies to it - see docs/todo/architecture/persistence-split and "
            + "docs/guides/checklists/new-query.md.");
    }

    /// <summary>
    /// The positive control. A comment stripper that quietly ate everything, or a signature list that matched nothing, would
    /// make the sweep above pass for the wrong reason - and it would pass silently, for ever.
    /// </summary>
    [Fact]
    public void TheSqlDetector_ShouldStillFindSqlInThePersistenceAdapter()
    {
        var found = SqlBearingFiles(AdapterRoot);

        found.Should().NotBeEmpty(
            "the adapter is where the SQL lives, so finding none there means this test class has stopped looking rather than "
            + "that the SQL has moved.");
    }

    /// <summary>
    /// The coverage exclusion that went with the SQL. Its wording says a unit test would only prove a mocked connection
    /// received a string - true of the adapter, and no longer true of anything in Application. An Application type carrying it
    /// is a handler that has quietly stopped being measured.
    /// </summary>
    [Fact]
    public void NoApplicationType_ShouldClaimTheAdaptersCoverageExclusion()
    {
        const string adapterJustification =
            "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and "
            + "verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.";

        var offenders = ProductionAssemblies.Application
            .GetTypes()
            .Where(type => !type.Name.Contains('<'))
            .Where(type =>
                type.GetCustomAttribute<ExcludeFromCodeCoverageAttribute>(inherit: false)?.Justification
                == adapterJustification)
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a handler in Application holds rules rather than SQL, so it is measured like any other code. Move the read to "
            + "ThePredictions.Persistence.SqlServer, where that justification is true, and unit test what is left.");
    }

    /// <summary>
    /// Belt and braces on the two above: Application cannot reach the database at all, so nothing in it can grow a statement
    /// back. The port's own declaration is the one file allowed to name it.
    /// </summary>
    [Fact]
    public void OnlyItsOwnDeclaration_ShouldNameTheReadConnection()
    {
        var offenders = ProductionAssemblies.SourceFiles(".cs")
            .Where(file => file.RelativePath.StartsWith(ApplicationRoot, StringComparison.Ordinal))
            .Where(file => !file.RelativePath.EndsWith("/Data/IApplicationReadDbConnection.cs", StringComparison.Ordinal))
            .Where(file => file.Text.Contains("IApplicationReadDbConnection", StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a query handler in Application takes a port of its own - one interface per query, returning row types - rather "
            + "than a database connection it would have to write SQL against.");
    }

    private static List<string> SqlBearingFiles(string root) =>
        ProductionAssemblies.SourceFiles(".cs")
            .Where(file => file.RelativePath.StartsWith(root, StringComparison.Ordinal))
            .Select(file => (file.RelativePath, Code: StripCommentsAndCharacters(file.Text)))
            .Where(file => SqlSignatures.Any(signature => signature.IsMatch(file.Code)))
            .Select(file => file.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Removes comments, leaving string literals in place - the SQL is in the literals, and the prose about the SQL is in the
    /// comments. Every file converted by the split explains in its remarks what the statement used to say, quoting the very
    /// keywords being searched for, so a sweep that read comments would flag the whole conversion.
    /// </summary>
    /// <remarks>
    /// Tracks string and character literals rather than stripping <c>//</c> everywhere, because a URL inside a literal would
    /// otherwise swallow the rest of its line.
    /// </remarks>
    private static string StripCommentsAndCharacters(string text)
    {
        var kept = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var remaining = text.Length - index;

            if (text[index] == '/' && remaining > 1 && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n')
                    index++;

                continue;
            }

            if (text[index] == '/' && remaining > 1 && text[index + 1] == '*')
            {
                var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 2;

                continue;
            }

            if (text[index] == '@' && remaining > 1 && text[index + 1] == '"')
            {
                index = CopyVerbatimString(text, index, kept);

                continue;
            }

            if (text[index] == '"')
            {
                index = CopyQuotedString(text, index, kept);

                continue;
            }

            kept.Append(text[index]);
            index++;
        }

        return kept.ToString();
    }

    /// <summary>Copies a <c>@"..."</c> literal, in which the only escape is a doubled quote.</summary>
    private static int CopyVerbatimString(string text, int start, StringBuilder kept)
    {
        kept.Append(text[start]).Append(text[start + 1]);
        var index = start + 2;

        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    kept.Append(text[index]).Append(text[index + 1]);
                    index += 2;

                    continue;
                }

                kept.Append(text[index]);

                return index + 1;
            }

            kept.Append(text[index]);
            index++;
        }

        return index;
    }

    /// <summary>Copies an ordinary <c>"..."</c> literal, honouring backslash escapes.</summary>
    private static int CopyQuotedString(string text, int start, StringBuilder kept)
    {
        kept.Append(text[start]);
        var index = start + 1;

        while (index < text.Length)
        {
            if (text[index] == '\\' && index + 1 < text.Length)
            {
                kept.Append(text[index]).Append(text[index + 1]);
                index += 2;

                continue;
            }

            if (text[index] == '"')
            {
                kept.Append(text[index]);

                return index + 1;
            }

            if (text[index] == '\n')
                return index;

            kept.Append(text[index]);
            index++;
        }

        return index;
    }
}
