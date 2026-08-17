namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// The traits CI filters on. Constants rather than literals so the convention tests and the classes they
/// police cannot drift apart - <see cref="Name"/> and <see cref="Value"/> must stay in step with
/// <c>.github/workflows/ci.yml</c>, <c>deploy-dev.yml</c>, <c>deploy-prod.yml</c> and <c>e2e.yml</c>.
/// </summary>
public static class E2ETrait
{
    public const string Name = "Category";
    public const string Value = "E2E";

    /// <summary>
    /// The second trait, naming how often a journey is worth running. See <see cref="TestLevel"/>.
    /// </summary>
    public const string LevelName = "Level";
}
