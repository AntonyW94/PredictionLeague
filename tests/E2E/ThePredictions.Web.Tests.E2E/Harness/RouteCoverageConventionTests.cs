using AwesomeAssertions;
using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// The goal for this suite is <b>every page</b>, and a goal nothing checks is a wish. This makes it a build
/// failure to add a route without deciding where it sits in the plan.
/// </summary>
/// <remarks>
/// It asserts the route is <i>accounted for</i>, not that a journey exists - an unticked box is a perfectly
/// good answer, and so is being listed under "deliberately not tested" with a reason. What it refuses is a
/// page nobody has thought about, which is how a suite silently stops covering an application that keeps
/// growing.
///
/// A markdown checklist is an unusual thing for a test to read. The alternative was an attribute or a registry
/// in code, which would have to be kept in step with the plan by hand and would put the list somewhere a
/// product decision cannot be read. The plan is where the ordering argument lives, so the plan is the list.
/// </remarks>
[Trait(E2ETrait.Name, E2ETrait.Value)]
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public class RouteCoverageConventionTests
{
    private static readonly string PlanPath = Path.Combine(
        E2ESettings.RepositoryRoot, "docs", "todo", "architecture", "e2e-testing", "README.md");

    [Fact]
    public void EveryRouteInTheApplication_ShouldBeAccountedForInThePlan()
    {
        var plan = ReadPlan();

        var unaccountedFor = WebClientSource.Routes()
            .Where(route => !plan.Contains($"`{route}`", StringComparison.Ordinal))
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToList();

        unaccountedFor.Should().BeEmpty(
            "this suite is aiming at every page, so a new route has to be placed in the plan's checklist - in "
            + "the fixture layer whose data it needs, or under \"deliberately not tested\" with the reason. "
            + $"Add it to {Relative(PlanPath)} as `/the/route`, backticks included, which is what this reads.");
    }

    /// <summary>
    /// The other direction. A checklist entry for a route that no longer exists is worse than a missing one:
    /// it reads as outstanding work for ever, and quietly inflates how much is left.
    /// </summary>
    [Fact]
    public void EveryRouteInThePlan_ShouldStillExistInTheApplication()
    {
        var routes = WebClientSource.Routes();

        // Only lines that are checklist entries, so prose mentioning a path is not mistaken for a claim that
        // the path is a route.
        var claimed = ReadPlan()
            .Split('\n')
            .Where(line => line.TrimStart().StartsWith("- [", StringComparison.Ordinal))
            .SelectMany(line => line.Split('`'))
            .Where(fragment => fragment.StartsWith('/'))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var stale = claimed
            .Where(route => !routes.Contains(route))
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            $"these are on the checklist in {Relative(PlanPath)} but the application no longer serves them. "
            + "Remove the entry, or correct it if the route was renamed - an entry for a page that does not "
            + "exist is permanently outstanding work that can never be done.");
    }

    private static string ReadPlan()
    {
        File.Exists(PlanPath).Should().BeTrue(
            $"the plan is the source of the checklist, and it should be at {Relative(PlanPath)}. If it moved, "
            + "point this test at it rather than deleting the test.");

        return File.ReadAllText(PlanPath);
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(E2ESettings.RepositoryRoot, path).Replace('\\', '/');
}
