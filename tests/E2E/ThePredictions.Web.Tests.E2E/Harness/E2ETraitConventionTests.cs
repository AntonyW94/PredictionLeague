using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// CI runs the unit suite with <c>--filter "Category!=Integration&amp;Category!=E2E"</c>, because this
/// project needs Docker and a browser rather than a compiler, and it owns no assembly for the 100% coverage
/// threshold to measure. Both deploy workflows exclude it for the same reason.
///
/// That split rests entirely on every test class here carrying the trait, and a forgotten one would go
/// missing from every job - passing CI while never executing. This is what stops that.
/// </summary>
[Trait(E2ETrait.Name, E2ETrait.Value)]
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public class E2ETraitConventionTests
{
    [Fact]
    public void EveryTestClass_ShouldBeTraitedAsE2E_SoTheCiFilterCannotLoseIt()
    {
        var untraited = TestClasses.InThisAssembly()
            .Where(type => !HasTrait(type, E2ETrait.Name, E2ETrait.Value))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        untraited.Should().BeEmpty(
            $"CI selects this project's tests with the {E2ETrait.Name}={E2ETrait.Value} trait, so an "
            + $"untraited class is run by no job at all - derive it from {nameof(E2ETestBase)}, which "
            + "carries the attribute, or add the attribute to the class.");
    }

    internal static bool HasTrait(Type type, string name, string value) =>
        type.GetCustomAttributes<TraitAttribute>(inherit: true)
            .Any(trait => trait.Name == name && trait.Value == value);
}
