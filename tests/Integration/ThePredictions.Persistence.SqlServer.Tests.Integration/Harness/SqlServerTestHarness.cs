using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.SqlServer.Data.Resilience;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Tests.Seeding;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;

/// <summary>
/// The SQL Server wiring a test needs, in one place: the connection seam, a transaction context, the
/// seeder and the inspector, plus the per-test reset.
///
/// Held as a plain object rather than a base class because there are now two kinds of test class here -
/// those deriving from <see cref="DatabaseTestBase"/>, and those deriving from a conformance base in
/// <c>ThePredictions.Persistence.Conformance</c> and so unable to inherit from anything local. Both
/// compose this, so the wiring exists once.
/// </summary>
internal sealed class SqlServerTestHarness
{
    private readonly SqlServerDatabaseFixture _fixture;

    internal SqlServerTestHarness(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
        ConnectionFactory = new TestDbConnectionFactory(fixture.ConnectionString);
        Seed = new TestDataSeeder(ConnectionFactory);
        Inspect = new SqlServerTestDataInspector(ConnectionFactory);
    }

    internal string ConnectionString => _fixture.ConnectionString;

    internal IDbConnectionFactory ConnectionFactory { get; }

    internal ITestDataSeeder Seed { get; }

    internal ITestDataInspector Inspect { get; }

    /// <summary>
    /// The real <see cref="DapperReadDbConnection"/> over the test container - the seam every query
    /// handler takes, and the one handler unit tests substitute, which is why the SQL behind it has
    /// never run under test. Retry and slow-query logging are left at their production defaults; the
    /// logger is silent because a passing query has nothing to report.
    /// </summary>
    internal IApplicationReadDbConnection ReadDbConnection => new DapperReadDbConnection(
        ConnectionFactory,
        new SqlRetryPolicy(Options.Create(new SqlRetryPolicyOptions()), NullLogger<SqlRetryPolicy>.Instance),
        Options.Create(new TimeoutSettings()),
        Options.Create(new QueryMonitoringSettings()),
        NullLogger<DapperReadDbConnection>.Instance);

    /// <summary>
    /// A fresh transaction context. Repositories take this and fall back to a new connection while no
    /// transaction is active, which is how they behave outside <c>TransactionBehaviour</c>.
    /// </summary>
    internal IDbTransactionContext NewTransactionContext() => new DbTransactionContext(ConnectionFactory);

    /// <summary>Deletes every row, leaving the migrated schema and the DbUp journal in place.</summary>
    internal async ValueTask ResetAsync() => await _fixture.ResetAsync();
}
