using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace ThePredictions.Conventions.Tests.Unit;

/// <summary>
/// The dependency direction is Presentation to Application to Domain, never the reverse. Today that
/// holds, but nothing stopped it: <c>dotnet build</c> is perfectly happy for someone to add a project
/// reference from Application to Infrastructure to reach a repository directly, and the CQRS split
/// ("commands use repositories, queries use IApplicationReadDbConnection") would quietly stop meaning
/// anything.
///
/// These read the <c>&lt;ProjectReference&gt;</c> elements out of the csproj files rather than
/// reflecting over <c>Assembly.GetReferencedAssemblies()</c>. That is deliberate: the compiler elides a
/// reference no code actually uses, so reflection would report a freshly added wrong reference as
/// absent and pass until someone wrote the first line of code against it. Reading the project files
/// catches it the moment the reference appears.
/// </summary>
public class LayerDependencyConventionTests
{
    private static IReadOnlyList<string> ProjectReferencesOf(string projectName)
    {
        var path = Path.Combine(ProductionAssemblies.RepositoryRoot, "src", projectName, $"{projectName}.csproj");

        File.Exists(path).Should().BeTrue($"expected to find the project file at '{path}'");

        return XDocument.Load(path)
            .Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Path.GetFileNameWithoutExtension(v!.Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static void AssertDoesNotReference(string projectName, params string[] forbidden)
    {
        var actual = ProjectReferencesOf(projectName);

        actual.Intersect(forbidden, StringComparer.Ordinal).Should().BeEmpty(
            $"{projectName} must not depend on {string.Join(", ", forbidden)}. "
            + $"It currently references: {(actual.Count == 0 ? "nothing" : string.Join(", ", actual))}.");
    }

    // Domain is the centre: it knows about nothing else in the solution. Note it does reference the
    // Microsoft.Extensions.Identity.Stores *package* (ApplicationUser derives from IdentityUser) - a
    // known and accepted trade-off rather than an oversight, so this rule covers project references only.
    [Fact]
    public void Domain_ShouldNotReferenceAnyOtherProjectInTheSolution()
    {
        ProjectReferencesOf("ThePredictions.Domain").Should().BeEmpty(
            "Domain sits at the centre of the dependency graph; anything it references becomes part of "
            + "the domain model's own dependencies.");
    }

    // The rule that matters most. Application defines the interfaces (IApplicationReadDbConnection, the
    // IXxxRepository set) and Infrastructure implements them; a reference the other way would let a
    // handler reach a concrete Dapper repository and make the abstraction decorative.
    [Fact]
    public void Application_ShouldNotReferenceInfrastructurePersistenceOrPresentation()
    {
        AssertDoesNotReference(
            "ThePredictions.Application",
            "ThePredictions.Infrastructure",
            "ThePredictions.Persistence.SqlServer",
            "ThePredictions.API",
            "ThePredictions.Web",
            "ThePredictions.Web.Client");
    }

    // The persistence split only means something if the two adapters stay peers. Infrastructure holds
    // the external-world adapters (Brevo, Stripe, the football API, SkiaSharp); a reference from there
    // into the SQL Server adapter would let a mail or payment concern reach a connection directly, and
    // swapping the database would stop being one call in the composition root. See
    // docs/todo/architecture/persistence-split/README.md.
    [Fact]
    public void Infrastructure_ShouldNotReferenceThePersistenceAdapter()
    {
        AssertDoesNotReference(
            "ThePredictions.Infrastructure",
            "ThePredictions.Persistence.SqlServer");
    }

    // The mirror of the rule above, and of Infrastructure_ShouldReferenceApplication: the adapter
    // implements Application's ports, so if this stopped holding the rules either side would be
    // guarding a relationship that no longer exists.
    [Fact]
    public void PersistenceAdapter_ShouldReferenceApplicationOnly()
    {
        ProjectReferencesOf("ThePredictions.Persistence.SqlServer")
            .Should().BeEquivalentTo(["ThePredictions.Application"],
                "the adapter needs Application's interfaces and nothing else - not Infrastructure, and "
                + "not Contracts, whose DTOs are the API's outward shape rather than a row type.");
    }

    [Fact]
    public void Contracts_ShouldOnlyReferenceDomain()
    {
        ProjectReferencesOf("ThePredictions.Contracts")
            .Should().BeSubsetOf(["ThePredictions.Domain"],
                "Contracts carries the DTOs shared between the API and the client, so anything it drags "
                + "in is dragged into the WebAssembly download too.");
    }

    [Fact]
    public void Validators_ShouldNotReferenceApplicationInfrastructureOrPresentation()
    {
        AssertDoesNotReference(
            "ThePredictions.Validators",
            "ThePredictions.Application",
            "ThePredictions.Infrastructure",
            "ThePredictions.API",
            "ThePredictions.Web",
            "ThePredictions.Web.Client");
    }

    // The browser gets Contracts and Validators only. A reference to Application or Infrastructure here
    // would ship server-side code - and its configuration and connection handling - to the client.
    [Fact]
    public void WebClient_ShouldNotReferenceApplicationInfrastructureOrApi()
    {
        AssertDoesNotReference(
            "ThePredictions.Web.Client",
            "ThePredictions.Application",
            "ThePredictions.Infrastructure",
            "ThePredictions.API");
    }

    [Fact]
    public void HostingShared_ShouldNotReferenceAnyOtherProjectInTheSolution()
    {
        ProjectReferencesOf("ThePredictions.Hosting.Shared").Should().BeEmpty(
            "Hosting.Shared is configuration plumbing shared by both hosts and must stay independent of "
            + "the application layers.");
    }

    // Infrastructure implementing Application's interfaces is the correct direction, and asserting it
    // keeps the pair of rules symmetrical - if this ever stopped holding, the rule above would be
    // guarding a relationship that no longer exists.
    [Fact]
    public void Infrastructure_ShouldReferenceApplication()
    {
        ProjectReferencesOf("ThePredictions.Infrastructure")
            .Should().Contain("ThePredictions.Application",
                "Infrastructure supplies the implementations behind Application's abstractions.");
    }
}
