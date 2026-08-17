using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// A run selects journeys by level - <c>--filter "Category=E2E&amp;(Level=Smoke|Level=Core)"</c> - so a
/// class with no level is a class no selection includes, and a class with a level nobody filters on is the
/// same thing spelled differently. Both would pass CI while never executing.
///
/// Exactly one level per class is also enforced, because a class in two would run twice in any selection
/// that included both.
/// </summary>
[Trait(E2ETrait.Name, E2ETrait.Value)]
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public class TestLevelConventionTests
{
    [Fact]
    public void EveryTestClass_ShouldDeclareExactlyOneLevel()
    {
        var wrong = TestClasses.InThisAssembly()
            .Select(type => (Type: type, Levels: LevelsOf(type)))
            .Where(x => x.Levels.Count != 1)
            .Select(x => x.Levels.Count == 0
                ? $"{x.Type.FullName}: no level"
                : $"{x.Type.FullName}: {x.Levels.Count} levels ({string.Join(", ", x.Levels)})")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        wrong.Should().BeEmpty(
            $"every test class needs exactly one [Trait(E2ETrait.LevelName, TestLevel.*)]. Without one it "
            + "is in no selection and runs nowhere; with two it runs twice whenever both are selected. "
            + $"Valid levels: {string.Join(", ", TestLevel.All)}.");
    }

    [Fact]
    public void NoTestClass_ShouldDeclareALevelNobodyFiltersOn()
    {
        var unrecognised = TestClasses.InThisAssembly()
            .SelectMany(type => LevelsOf(type).Select(level => (type, level)))
            .Where(x => !TestLevel.All.Contains(x.level, StringComparer.Ordinal))
            .Select(x => $"{x.type.FullName}: \"{x.level}\"")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        unrecognised.Should().BeEmpty(
            $"the only levels any run selects are {string.Join(", ", TestLevel.All)}. Use TestLevel's "
            + "constants rather than a literal, and add a new level there - and to the workflow's "
            + "tickboxes - if one is genuinely needed.");
    }

    /// <summary>
    /// The level goes on the class, and only on the class.
    /// </summary>
    /// <remarks>
    /// This rule earned itself immediately: the first journey was written with the trait on its
    /// <c>[Fact]</c>, and the sibling test above reported it as having no level. That was the right answer
    /// but a confusing one, because a method-level trait <i>does</i> work for <c>dotnet test --filter</c> -
    /// xUnit merges class and method traits into each test case. So the level would have selected correctly
    /// while being invisible to the convention that is supposed to guarantee it, which is the worst of both.
    /// One place to look, enforced.
    /// </remarks>
    [Fact]
    public void NoTestMethod_ShouldCarryItsOwnLevel()
    {
        var offenders = TestClasses.InThisAssembly()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetCustomAttributes<TraitAttribute>(inherit: true)
                    .Any(trait => trait.Name == E2ETrait.LevelName))
                .Select(method => $"{type.FullName}.{method.Name}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "the level belongs on the test class, not on individual tests. A method-level trait does filter "
            + "correctly, which is the trap - it would work while being invisible to the convention above, "
            + "leaving the guarantee resting on nothing.");
    }

    private static List<string> LevelsOf(Type type) =>
        type.GetCustomAttributes<TraitAttribute>(inherit: true)
            .Where(trait => trait.Name == E2ETrait.LevelName)
            .Select(trait => trait.Value)
            .ToList();
}
