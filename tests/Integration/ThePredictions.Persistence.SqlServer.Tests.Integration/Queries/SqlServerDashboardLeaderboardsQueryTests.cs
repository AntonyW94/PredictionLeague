using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Dashboard;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>Runs <see cref="DashboardLeaderboardsQueryConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerDashboardLeaderboardsQueryTests(SqlServerDatabaseFixture fixture)
    : DashboardLeaderboardsQueryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    protected override IDashboardLeaderboardsQuery Query => new DashboardLeaderboardsQuery(_harness.ReadDbConnection);

    protected override ITestDataSeeder Seed => _harness.Seed;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
