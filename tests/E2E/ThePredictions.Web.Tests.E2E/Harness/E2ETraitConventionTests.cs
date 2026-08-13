using System.Reflection;
using FluentAssertions;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// CI runs the unit suite with <c>--filter "Category!=Integration&amp;Category!=E2E"</c>, because this project
/// needs a deployed site and a browser rather than a compiler, and it owns no assembly for the 100% coverage
/// threshold to measure. The deploy workflows exclude it for a second reason: Stage 1 reports on a deployment
/// *after* it lands, so running these tests before one would test the previous build.
///
/// That split rests entirely on every test class here carrying the trait, and a forgotten one would go
/// missing from every job - passing CI while never executing. This test is what stops that.
/// </summary>
[Trait(E2ETrait.Name, E2ETrait.Value)]
public class E2ETraitConventionTests
{
    [Fact]
    public void EveryTestClass_ShouldBeTraitedAsE2E_SoTheCiFilterCannotLoseIt()
    {
        var untraited = typeof(E2ETraitConventionTests).Assembly
            .GetTypes()
            .Where(HasTests)
            .Where(type => !IsTraitedAsE2E(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        untraited.Should().BeEmpty(
            $"CI selects this project's tests with the {E2ETrait.Name}={E2ETrait.Value} trait, so an untraited "
            + $"class is run by neither job - derive it from {nameof(E2ETestBase)}, which carries the attribute, "
            + "or add the attribute to the class.");
    }

    // Deliberately not DeclaredOnly, and inherit: true on the attribute lookup: a class deriving from
    // E2ETestBase inherits the trait, and that is the intended way to get it.
    private static bool HasTests(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(method => method.GetCustomAttribute<FactAttribute>(inherit: true) != null
                           || method.GetCustomAttribute<TheoryAttribute>(inherit: true) != null);

    private static bool IsTraitedAsE2E(Type type) =>
        type.GetCustomAttributes<TraitAttribute>(inherit: true)
            .Any(trait => trait.Name == E2ETrait.Name && trait.Value == E2ETrait.Value);
}
