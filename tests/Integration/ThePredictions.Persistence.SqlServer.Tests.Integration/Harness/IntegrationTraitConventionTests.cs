using System.Reflection;
using FluentAssertions;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;

/// <summary>
/// CI runs the unit suite with <c>--filter "Category!=Integration"</c> so it does not have to wait for a
/// SQL Server container, then runs this project on its own. That split rests entirely on every test
/// class here carrying the trait, and a forgotten one would go missing from both runs - passing CI while
/// never executing. This test is what stops that.
/// </summary>
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class IntegrationTraitConventionTests
{
    [Fact]
    public void EveryTestClass_ShouldBeTraitedAsIntegration_SoTheCiFilterCannotLoseIt()
    {
        var untraited = typeof(IntegrationTraitConventionTests).Assembly
            .GetTypes()
            .Where(HasTests)
            .Where(t => !IsTraitedAsIntegration(t))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        untraited.Should().BeEmpty(
            $"CI selects this project's tests with the {IntegrationTrait.Name}={IntegrationTrait.Value} trait, so an "
            + "untraited class is run by neither job - add the attribute to the class.");
    }

    private static bool HasTests(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(m => m.GetCustomAttribute<FactAttribute>(inherit: true) != null
                      || m.GetCustomAttribute<TheoryAttribute>(inherit: true) != null);

    private static bool IsTraitedAsIntegration(Type type) =>
        type.GetCustomAttributes<TraitAttribute>(inherit: true)
            .Any(t => t.Name == IntegrationTrait.Name && t.Value == IntegrationTrait.Value);
}
