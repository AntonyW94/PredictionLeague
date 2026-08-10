namespace ThePredictions.Infrastructure.Tests.Integration.Harness;

/// <summary>
/// The trait CI filters on. Constants rather than literals so the convention test and the classes it
/// polices cannot drift apart - the value here must stay in step with <c>.github/workflows/ci.yml</c>.
/// </summary>
public static class IntegrationTrait
{
    public const string Name = "Category";
    public const string Value = "Integration";
}
