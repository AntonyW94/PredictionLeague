using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Seasons;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>Runs <see cref="SeasonsQueryConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerSeasonsQueryTests(SqlServerDatabaseFixture fixture)
    : SeasonsQueryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    protected override ISeasonsQuery Query => new SeasonsQuery(_harness.ReadDbConnection);

    protected override ITestDataSeeder Seed => _harness.Seed;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
