using ThePredictions.Application.Repositories;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Repositories;
using ThePredictions.Persistence.SqlServer.Repositories;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Repositories;

/// <summary>Runs <see cref="LeagueRepositoryScoringConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerLeagueRepositoryScoringTests(SqlServerDatabaseFixture fixture)
    : LeagueRepositoryScoringConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    // The clock is only read when the repository stamps a row it creates, which none of these tests reach - but it is a
    // real instant rather than a null so that a future test which does reach it gets a sane value.
    protected override ILeagueRepository Repository =>
        new LeagueRepository(
            _harness.ConnectionFactory,
            _harness.NewTransactionContext(),
            new TestDateTimeProvider(new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc)));

    protected override ITestDataSeeder Seed => _harness.Seed;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
