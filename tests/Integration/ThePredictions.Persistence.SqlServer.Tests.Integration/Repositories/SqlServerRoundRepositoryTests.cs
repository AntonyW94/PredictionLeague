using ThePredictions.Application.Repositories;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Repositories;
using ThePredictions.Persistence.SqlServer.Repositories;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Repositories;

/// <summary>
/// Runs <see cref="RoundRepositoryConformanceTests"/> against SQL Server. The whole class is the three
/// members the base needs - every test lives in the base, written once, so a second adapter gets the same
/// suite by writing a class this size.
/// </summary>
/// <remarks>
/// The <c>[Collection]</c> attribute is declared here rather than inherited: unlike a
/// <see cref="DatabaseTestBase"/> subclass, a conformance subclass derives from another assembly and so
/// cannot pick it up. Forgetting it fails immediately and legibly ("constructor parameters did not have
/// matching fixture data"), which is why it needs no convention test of its own.
/// </remarks>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerRoundRepositoryTests(SqlServerDatabaseFixture fixture)
    : RoundRepositoryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    // A fresh repository per access, with no transaction in progress - how it behaves outside
    // TransactionBehaviour.
    protected override IRoundRepository Repository =>
        new RoundRepository(_harness.ConnectionFactory, _harness.NewTransactionContext());

    protected override ITestDataSeeder Seed => _harness.Seed;

    protected override ITestDataInspector Inspect => _harness.Inspect;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
