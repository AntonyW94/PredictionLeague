namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// The trait CI filters on. Constants rather than literals so the convention test and the classes it
/// polices cannot drift apart - the value here must stay in step with <c>.github/workflows/ci.yml</c>,
/// <c>deploy-dev.yml</c>, <c>deploy-prod.yml</c> and <c>e2e-dev.yml</c>.
/// </summary>
public static class E2ETrait
{
    public const string Name = "Category";
    public const string Value = "E2E";
}
