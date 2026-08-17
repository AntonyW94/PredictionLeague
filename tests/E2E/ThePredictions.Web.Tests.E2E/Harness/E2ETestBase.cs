using System.Runtime.CompilerServices;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Base for every browser test in this assembly. Carries the category trait, so a derived class is
/// selectable by CI without having to remember the attribute; the <b>level</b> trait is deliberately not
/// inherited, because every class has to choose one for itself and
/// <see cref="TestLevelConventionTests"/> fails the build if it does not.
/// </summary>
[Collection(StackCollection.Name)]
[Trait(E2ETrait.Name, E2ETrait.Value)]
public abstract class E2ETestBase(StackFixture stack)
{
    /// <summary>
    /// The stack's database, for a test class that has to arrange a season and league of its own before its
    /// journeys can run. Read it in <c>IAsyncLifetime.InitializeAsync</c>, not from inside a test - seeding
    /// per test would arrange the same world several times over.
    /// </summary>
    internal TestDatabase Database => stack.Database;

    /// <summary>Opens an isolated browser context for the calling test.</summary>
    internal async Task<BrowserSession> StartSessionAsync([CallerMemberName] string testName = "") =>
        // Qualified by the class, because the trace file is named after this and two actors can reasonably
        // want the same test name.
        await stack.NewSessionAsync($"{GetType().Name}.{testName}");
}
