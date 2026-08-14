namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// How often a journey is worth running. Every test class carries exactly one of these as a
/// <c>[Trait(E2ETrait.LevelName, ...)]</c>, and a run selects levels by combining them in the filter:
///
/// <code>dotnet test --filter "Category=E2E&amp;(Level=Smoke|Level=Core)"</code>
///
/// Exactly one per class, never several - a class in two levels would run twice in a selection that
/// included both.
///
/// The names describe the <b>test</b>; the run names describe the <b>selection</b>. Hence
/// <see cref="Extended"/> rather than "Full", which would leave you saying "the full run runs the Full
/// tests plus the others".
/// </summary>
public static class TestLevel
{
    /// <summary>Cannot be broken without the site being unusable. Runs on everything.</summary>
    public const string Smoke = "Smoke";

    /// <summary>Features used most weeks.</summary>
    public const string Core = "Core";

    /// <summary>Rarely used, but still has to work. Typically left to the scheduled run.</summary>
    public const string Extended = "Extended";

    /// <summary>Every valid level, for the convention test that rejects anything else.</summary>
    public static readonly IReadOnlyList<string> All = [Smoke, Core, Extended];
}
