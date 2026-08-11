using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Users;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>Runs <see cref="AdminUsersQueryConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerAdminUsersQueryTests(SqlServerDatabaseFixture fixture)
    : AdminUsersQueryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    protected override IAdminUsersQuery Query => new AdminUsersQuery(_harness.ReadDbConnection);

    protected override ITestDataSeeder Seed => _harness.Seed;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
