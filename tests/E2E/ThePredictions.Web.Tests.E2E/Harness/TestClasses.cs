using System.Reflection;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// The set of classes the convention tests police: every type in this assembly that actually declares or
/// inherits a runnable test.
/// </summary>
internal static class TestClasses
{
    /// <summary>
    /// Deliberately not <c>DeclaredOnly</c>, and <c>inherit: true</c> on the attribute lookup: a class that
    /// inherits its tests from a base declares no <c>[Fact]</c> of its own, and would otherwise escape the
    /// sweep entirely - which is exactly the class most likely to be added without a trait.
    /// </summary>
    internal static IEnumerable<Type> InThisAssembly() =>
        typeof(TestClasses).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(HasTests);

    private static bool HasTests(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(method => method.GetCustomAttribute<FactAttribute>(inherit: true) != null
                           || method.GetCustomAttribute<TheoryAttribute>(inherit: true) != null);
}
