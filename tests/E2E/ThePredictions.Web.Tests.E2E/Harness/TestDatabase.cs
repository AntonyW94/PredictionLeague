using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Tests.Seeding;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// The throwaway SQL Server the application under test runs against: one container per run, a database
/// created with production's collation, the committed migrations applied, and the minimum data seeded.
/// </summary>
/// <remarks>
/// No Respawn here, unlike the integration suite. That suite wipes between tests because each one arranges
/// its own rows; this one shares a single running application across the whole run, so wiping underneath it
/// would pull the rug out from a live process. When journeys that mutate data arrive, the isolation
/// decision recorded in the plan needs making first - a league per test class, most likely - rather than
/// reaching for a reset that would fight the application's own connection pool.
/// </remarks>
internal sealed class TestDatabase : IAsyncDisposable
{
    private const string DatabaseName = "ThePredictionsE2E";

    // Production's default collation. 0001_Baseline.sql omits every COLLATE clause on purpose (ADR-0013),
    // so a database created with a different default would give the whole schema different string
    // comparison and sort semantics from production. Stated rather than taken on trust from the image.
    private const string Collation = "SQL_Latin1_General_CP1_CI_AS";

    // Pinned rather than floating on :latest, so a new image release cannot change what CI tests. Same
    // tag the integration suite pulls, so a run of both shares one image download.
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(SqlServerImage).Build();

    /// <summary>Connection string for the migrated database, for the application and the seed alike.</summary>
    internal string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Arranges rows for a journey. The same seeder the integration suite arranges with, so there is one
    /// place that knows the schema rather than one per suite - see <see cref="TestDataSeeder"/>.
    /// </summary>
    internal ITestDataSeeder Seed { get; private set; } = null!;

    internal async Task StartAsync()
    {
        await _container.StartAsync();

        await CreateDatabaseAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;

        MigrationRunner.Apply(ConnectionString);

        Seed = new TestDataSeeder(new TestDbConnectionFactory(ConnectionString));

        await SeedPlayerAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Pooled connections outlive the container otherwise, and a later run could be handed a dead one
        // from the pool if the port were reused.
        SqlConnection.ClearAllPools();

        await _container.DisposeAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        // The container hands out a connection string pointing at master, which is where CREATE DATABASE
        // has to run from.
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{DatabaseName}] COLLATE {Collation};";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// One user, which is all a login journey needs.
    /// </summary>
    /// <remarks>
    /// Through the shared seeder rather than SQL of its own. Arranging with SQL written here would mean a
    /// second place knowing the <c>AspNetUsers</c> column list, and that number would grow with every table
    /// a journey needs - seasons, leagues, rounds, predictions. The seeder already knows all of them.
    ///
    /// A password is passed, which is the one thing a browser journey needs and no query test does: it makes
    /// the seeder write a real hash and a security stamp, both of which Identity requires to accept a
    /// sign-in. The Identity <i>roles</i> are still not seeded at all - the <c>DatabaseInitialiser</c> hosted
    /// service creates them from the <c>ApplicationUserRole</c> enum when the application starts.
    /// </remarks>
    private async Task SeedPlayerAsync() =>
        await Seed.AddUserAsync(
            E2ESettings.PlayerFirstName,
            E2ESettings.PlayerLastName,
            E2ESettings.PlayerEmail,
            E2ESettings.PlayerPassword);
}
