using Microsoft.Data.SqlClient;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using ThePredictions.Persistence.SqlServer.Migrations;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;

/// <summary>
/// One throwaway SQL Server for the whole test run: the container starts once, a database is created
/// with production's collation, and the committed DbUp migrations build its schema. Between tests
/// Respawn deletes every row and leaves the schema alone, so each test arranges from empty without
/// paying for a container or a migration run.
///
/// Real SQL Server rather than SQLite is the deliberate choice recorded in the test-suite plan. The
/// queries these tests exist to pin use <c>RANK() OVER</c>, <c>CROSS APPLY</c>, <c>MERGE</c>,
/// <c>GETUTCDATE()</c> and <c>CAST(... AS bit)</c>; SQLite either rejects those or evaluates them
/// differently, so a SQLite suite would go green while proving nothing. It also would not reproduce
/// the <c>int</c>/<c>bigint</c> distinction behind the July 2026 leaderboard outage.
///
/// Requires a working Docker daemon. Tests are skipped by nothing and will fail loudly without one -
/// that is intended, because silently skipping a data-loss guard is worse than a red build.
/// </summary>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private const string DatabaseName = "ThePredictionsIntegration";

    // Production's default collation. 0001_Baseline.sql omits every COLLATE clause on purpose
    // (ADR-0013), so a database created with a different default would give the whole schema
    // different string comparison and sort semantics from production. Stated here rather than taken
    // on trust from the image default.
    private const string Collation = "SQL_Latin1_General_CP1_CI_AS";

    // Pinned rather than floating on :latest, so a new image release cannot change what CI tests.
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(SqlServerImage).Build();

    private Respawner? _respawner;

    /// <summary>Connection string for the migrated test database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await CreateTestDatabaseAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;

        MigrationRunner.Apply(ConnectionString);

        _respawner = await CreateRespawnerAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Pooled connections outlive the container otherwise, and the next run would hand out a dead
        // one from the pool if the port were reused.
        SqlConnection.ClearAllPools();

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Deletes every row in every table, leaving the migrated schema and the DbUp journal in place.
    /// Called before each test by <see cref="DatabaseTestBase"/>.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_respawner == null)
            throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run.");

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }

    private async Task CreateTestDatabaseAsync()
    {
        // The container hands out a connection string pointing at master, which is where a CREATE
        // DATABASE has to run from.
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{DatabaseName}] COLLATE {Collation};";
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Respawner> CreateRespawnerAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        return await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["dbo"],
            // Clearing the journal would make DbUp re-run every script on the next reset, and the
            // schema is not what a test is allowed to change.
            TablesToIgnore = [new Table(MigrationScripts.JournalSchema, MigrationScripts.JournalTable)]
        });
    }
}
