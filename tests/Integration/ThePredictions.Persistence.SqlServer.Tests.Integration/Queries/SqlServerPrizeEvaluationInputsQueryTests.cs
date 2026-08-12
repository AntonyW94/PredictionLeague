using ThePredictions.Application.Common.Prizes;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Prizes;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>Runs <see cref="PrizeEvaluationInputsQueryConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerPrizeEvaluationInputsQueryTests(SqlServerDatabaseFixture fixture)
    : PrizeEvaluationInputsQueryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    protected override IPrizeEvaluationInputsQuery Query => new PrizeEvaluationInputsQuery(_harness.ReadDbConnection);

    protected override ITestDataSeeder Seed => _harness.Seed;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
