using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Every test in this assembly joins this collection, which is what makes the stack shared and the tests
/// sequential.
/// </summary>
/// <remarks>
/// Sequential is a starting position, not a settled decision. It is right while every journey only reads:
/// one container, one schema, one running application, no interference. It stops being right as soon as a
/// journey writes - a test that submits a prediction would break a test asserting none exist. The plan
/// records the choice that then has to be made, which is a season and league per test class rather than a
/// database reset, because resetting underneath a live application would fight its connection pool.
/// </remarks>
[CollectionDefinition(Name)]
public class StackCollection : ICollectionFixture<StackFixture>
{
    public const string Name = "Application stack";
}
