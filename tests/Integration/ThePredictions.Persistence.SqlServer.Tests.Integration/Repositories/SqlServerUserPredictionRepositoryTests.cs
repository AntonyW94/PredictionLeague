using ThePredictions.Application.Repositories;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Repositories;
using ThePredictions.Persistence.SqlServer.Repositories;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Repositories;

/// <summary>Runs <see cref="UserPredictionRepositoryConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerUserPredictionRepositoryTests(SqlServerDatabaseFixture fixture)
    : UserPredictionRepositoryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    protected override IUserPredictionRepository Repository =>
        new UserPredictionRepository(_harness.ConnectionFactory, _harness.NewTransactionContext());

    protected override ITestDataSeeder Seed => _harness.Seed;

    protected override ITestDataInspector Inspect => _harness.Inspect;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
