using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace ThePredictions.Conventions.Tests.Unit;

/// <summary>
/// The solution reports 100% line and branch coverage, and that figure is only as honest as the
/// <c>[ExcludeFromCodeCoverage]</c> attributes behind it. <c>docs/guides/testing.md</c> requires every
/// one to carry a <c>Justification</c>, worded consistently so the categories can be counted and
/// questioned - but nothing enforced it, because no off-the-shelf analyser can (see EctManager's
/// ADR-0082, which investigated exactly this and found none). These tests are that enforcement.
///
/// What this does and does not buy is worth being clear about: it stops an unexplained exclusion and
/// stops the agreed wordings drifting into near-duplicates. It cannot tell that a justification is
/// *wrong* for the code it sits on - the audit that added these tests found five exclusions that all
/// carried perfectly well-formed justifications describing code they did not match. That remains a
/// reading job.
/// </summary>
public class CoverageExclusionConventionTests
{
    /// <summary>
    /// The agreed wordings. Adding one is a deliberate act that shows up in review, which is the whole
    /// point - a new category should be argued for, not typed. The five recurring phrasings documented
    /// in testing.md are here alongside the narrower one-off reasons that file explicitly permits.
    /// </summary>
    private static readonly string[] ApprovedJustifications =
    [
        "ASP.NET Identity store over Dapper: SQL plus framework plumbing, exercised end to end.",
        "ASP.NET Identity wrapper: forwards to UserManager and maps its result, exercised end to end.",
        "Blazor component: rendering behaviour, untestable without bUnit.",
        "Browser interop: a pass-through to JavaScript with no logic of its own.",
        "Container registration: verified by ThePredictions.Composition.Tests.Unit, which resolves every handler from the real container.",
        "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.",
        "Dapper row type: properties only, no logic to test.",
        "Data-only contract: properties only, no logic to test.",
        "Data-only type: properties only, no logic to test.",
        "Data-only view model: properties only, no logic to test.",
        "Database plumbing: connection, transaction and type-handler wiring with no branching logic of its own.",
        "Exception type: a message and an inner exception, no logic to test.",
        "Football API response shape: properties only, deserialised straight from the provider.",
        "HTTP message handler plumbing: no branching logic of its own.",
        "Health check: endpoint wiring and a live dependency probe, verified by hitting the endpoint.",
        "Image renderer: SkiaSharp drawing plus HTTP asset fetches. Correctness is visual, not assertable in a unit test.",
        "MediatR request record: properties only, no logic to test.",
        "Middleware registration: one UseMiddleware call, exercised end to end.",
        "Middleware wiring: registration and header plumbing, exercised end to end.",
        "Options type bound from configuration: properties only, no logic to test.",
        "Orchestrates repository reads and the email client; the formatting rules it calls are tested separately.",
        "Parameterless constructor for Dapper hydration: no logic to test.",
        "Polly pipeline configuration: declarative retry, circuit-breaker and timeout wiring with no logic of its own, verified against the live API.",
        "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.",
        "Repository composition over SQL: no branching logic of its own.",
        "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.",
        "Returns DateTime.UtcNow: nothing to assert that is not a tautology.",
        "Set only by Dapper when hydrating from the database; the only constructor is private, so nothing else can reach it.",
        "Third-party API client: a thin call into an external SDK, verified against the live service.",
        "Third-party API client: caches a live Brevo template listing. The parameter extraction it delegates to is tested separately.",
        "Typed HttpClient wrapper: forwards to an API endpoint and deserialises the reply."
    ];

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static IEnumerable<(string Description, ExcludeFromCodeCoverageAttribute Attribute)> AllExclusions()
    {
        foreach (var assembly in ProductionAssemblies.All)
        {
            foreach (var type in assembly.GetTypes().Where(t => !IsCompilerGenerated(t)))
            {
                var onType = type.GetCustomAttribute<ExcludeFromCodeCoverageAttribute>(inherit: false);
                if (onType != null)
                    yield return ($"{assembly.GetName().Name}: type {type.FullName}", onType);

                foreach (var member in type.GetMembers(AllDeclared).Where(m => !IsCompilerGenerated(m)))
                {
                    var onMember = member.GetCustomAttribute<ExcludeFromCodeCoverageAttribute>(inherit: false);
                    if (onMember != null)
                        yield return ($"{assembly.GetName().Name}: {type.FullName}.{member.Name}", onMember);
                }
            }
        }
    }

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.GetCustomAttribute<CompilerGeneratedAttribute>(inherit: false) != null
        || member.Name.Contains('<');

    [Fact]
    public void EveryExclusion_ShouldCarryAJustification()
    {
        var unexplained = AllExclusions()
            .Where(e => string.IsNullOrWhiteSpace(e.Attribute.Justification))
            .Select(e => e.Description)
            .OrderBy(d => d)
            .ToList();

        unexplained.Should().BeEmpty(
            "[ExcludeFromCodeCoverage] must say why - an exclusion without a reason turns the 100% gate "
            + "into a to-do marker. See docs/guides/testing.md.");
    }

    [Fact]
    public void EveryExclusion_ShouldUseAnApprovedJustificationWording()
    {
        var unapproved = AllExclusions()
            .Where(e => !string.IsNullOrWhiteSpace(e.Attribute.Justification))
            .Where(e => !ApprovedJustifications.Contains(e.Attribute.Justification))
            .Select(e => $"{e.Description}\n    -> \"{e.Attribute.Justification}\"")
            .OrderBy(d => d)
            .ToList();

        unapproved.Should().BeEmpty(
            "the recurring justifications are worded identically so they can be grepped and counted. If "
            + "this exclusion genuinely needs new wording, add it to ApprovedJustifications in this file "
            + "so the new category is visible in review.");
    }

    // A type with a test file is a type we test, so excluding it hides passing tests and makes them look
    // pointless. testing.md calls this out as the rule that matters most for query handlers, where the
    // category is excluded wholesale but several members of it have real tests.
    [Fact]
    public void NoExcludedType_ShouldAlreadyHaveATestFile()
    {
        var testFileNames = Directory
            .EnumerateFiles(Path.Combine(ProductionAssemblies.RepositoryRoot, "tests"), "*Tests.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

        var excludedTypesWithTests = ProductionAssemblies.All
            .SelectMany(a => a.GetTypes())
            .Where(t => !IsCompilerGenerated(t))
            .Where(t => t.GetCustomAttribute<ExcludeFromCodeCoverageAttribute>(inherit: false) != null)
            .Where(t => testFileNames.Contains($"{t.Name}Tests"))
            .Select(t => t.FullName!)
            .OrderBy(n => n)
            .ToList();

        excludedTypesWithTests.Should().BeEmpty(
            "a type with a test file is measured, not excluded - remove the attribute or delete the tests.");
    }
}
