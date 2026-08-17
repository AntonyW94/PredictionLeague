using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

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

    internal async Task StartAsync()
    {
        await _container.StartAsync();

        await CreateDatabaseAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;

        MigrationRunner.Apply(ConnectionString);

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
    /// Written as SQL rather than through the application's registration endpoint deliberately: a test
    /// should not arrange through the path it is about to assert on, and registering would drag in the
    /// email-confirmation gate. The Identity <i>roles</i> are not seeded here at all - the
    /// <c>DatabaseInitialiser</c> hosted service creates them from the <c>ApplicationUserRole</c> enum
    /// when the application starts, so they arrive on their own.
    ///
    /// <c>EmailConfirmed</c> is true because an unconfirmed account is a different journey, and
    /// <c>SecurityStamp</c> is set because Identity requires one to validate a sign-in.
    /// </remarks>
    private async Task SeedPlayerAsync()
    {
        var passwordHash = new PasswordHasher<object>().HashPassword(new object(), E2ESettings.PlayerPassword);

        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync(
            """
            INSERT INTO [AspNetUsers] (
                [Id],
                [UserName],
                [NormalizedUserName],
                [Email],
                [NormalizedEmail],
                [EmailConfirmed],
                [PasswordHash],
                [SecurityStamp],
                [ConcurrencyStamp],
                [PhoneNumber],
                [PhoneNumberConfirmed],
                [TwoFactorEnabled],
                [LockoutEnd],
                [LockoutEnabled],
                [AccessFailedCount],
                [FirstName],
                [LastName]
            )
            VALUES (
                @Id,
                @UserName,
                @NormalizedUserName,
                @Email,
                @NormalizedEmail,
                @EmailConfirmed,
                @PasswordHash,
                @SecurityStamp,
                @ConcurrencyStamp,
                @PhoneNumber,
                @PhoneNumberConfirmed,
                @TwoFactorEnabled,
                @LockoutEnd,
                @LockoutEnabled,
                @AccessFailedCount,
                @FirstName,
                @LastName
            )
            """,
            new
            {
                Id = Guid.NewGuid().ToString(),
                UserName = E2ESettings.PlayerEmail,
                NormalizedUserName = E2ESettings.PlayerEmail.ToUpperInvariant(),
                Email = E2ESettings.PlayerEmail,
                NormalizedEmail = E2ESettings.PlayerEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                PasswordHash = passwordHash,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                PhoneNumber = (string?)null,
                PhoneNumberConfirmed = false,
                TwoFactorEnabled = false,
                LockoutEnd = (DateTimeOffset?)null,
                LockoutEnabled = false,
                AccessFailedCount = 0,
                FirstName = E2ESettings.PlayerFirstName,
                LastName = E2ESettings.PlayerLastName
            });
    }
}
