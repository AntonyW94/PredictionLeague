using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using ThePredictions.Domain.Common.Enumerations;
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

    /// <summary>
    /// A season, a league, and a player who is an approved member of it holding a Season Pass - everything a
    /// journey needs to reach a league page and a leaderboard.
    /// </summary>
    /// <param name="scope">
    /// The calling test class's name. It is woven into the player's email and the league's name, which is
    /// what makes the arrangement <b>per test class</b> rather than shared.
    /// </param>
    /// <remarks>
    /// <para>
    /// Per class, deliberately, and this is the isolation decision the plan said had to be made before a
    /// second journey. Everything so far only reads, so one shared arrangement would have been fine; the
    /// first journey that <i>writes</i> breaks that, because a test submitting a prediction breaks a test
    /// asserting none exist. Respawn - which is how the integration suite gets its isolation - is not an
    /// option here: it deletes every row, and there is a live application holding a connection pool over this
    /// database for the whole run. Giving each class its own season, league and player costs a handful of
    /// inserts and means a journey can write without consulting anybody.
    /// </para>
    /// <para>
    /// It also leaves the door open. The suite runs serially today because one application process and
    /// parallel WebAssembly browsers on a two-core runner would thrash, not because the data forbids it - so
    /// parallelising later is a change to the collection definition rather than to every assertion.
    /// </para>
    /// </remarks>
    internal async Task<SeededLeague> SeedLeagueAsync(string scope)
    {
        var email = $"{scope}@e2e.test".ToLowerInvariant();

        var playerUserId = await Seed.AddUserAsync("Ellie", scope, email, E2ESettings.PlayerPassword);

        // A separate administrator, so the player under test is a plain member. Making them the league's own
        // admin would quietly change what the dashboard renders - the pending-members and pending-requests
        // tiles appear for an admin - and a journey should see what an ordinary player sees.
        var administratorUserId = await Seed.AddUserAsync("Admin", scope);

        var competitionId = await Seed.AddCompetitionAsync($"E2E{Math.Abs(scope.GetHashCode()) % 10000}");
        var seasonId = await Seed.AddSeasonAsync(competitionId, $"{scope} Season");

        var homeTeamId = await Seed.AddTeamAsync($"{scope} Home", "HOM");
        var awayTeamId = await Seed.AddTeamAsync($"{scope} Away", "AWY");

        // entryDeadlineUtc left null on purpose. The league dashboard renders a countdown instead of its
        // content while `EntryDeadlineUtc is { } deadline && UtcNow < deadline`, and null is the column's
        // default and reads as "entry is not still open".
        var leagueId = await Seed.AddLeagueAsync(seasonId, administratorUserId, $"{scope} League");

        await Seed.AddLeagueMemberAsync(leagueId, playerUserId);
        await Seed.AddSeasonPassAsync(playerUserId, seasonId, source: SeasonPassSource.Free);

        // At least one non-Draft round, and this is the other half of escaping the countdown: the dashboard
        // also falls back to it when `!ViewableRounds.Any()`, and GetLeagueDashboardQueryHandler counts a
        // round as viewable only when its status is not Draft. Completed rather than Published so the page
        // renders its settled layout.
        var roundId = await Seed.AddRoundAsync(
            seasonId,
            roundNumber: 1,
            deadlineUtc: DateTime.UtcNow.AddDays(-7),
            status: RoundStatus.Completed,
            startDateUtc: DateTime.UtcNow.AddDays(-8),
            completedDateUtc: DateTime.UtcNow.AddDays(-6));

        await Seed.AddMatchAsync(roundId, homeTeamId, awayTeamId, DateTime.UtcNow.AddDays(-7));

        return new SeededLeague(
            seasonId, leagueId, $"{scope} League", email, E2ESettings.PlayerPassword, roundId);
    }

    /// <summary>
    /// A <b>private</b> league the player is deliberately <i>not</i> a member of, plus a Season Pass for its
    /// season - the arrangement needed to exercise joining by entry code.
    /// </summary>
    /// <remarks>
    /// Three details the join flow will not work without, each read out of the code rather than guessed:
    /// <list type="bullet">
    ///   <item>The Season Pass, because <c>JoinLeagueCommandHandler</c> calls
    ///         <c>EnsureCanParticipateAsync</c> and the acquire-first gate refuses a joiner without one.</item>
    ///   <item>No membership, because the dashboard's private-league check is a <c>NOT EXISTS</c> against
    ///         <c>LeagueMembers</c> - already being in it hides the button that opens the modal.</item>
    ///   <item>A six-character alphanumeric code, because <c>JoinLeagueRequestValidator</c> enforces exactly
    ///         that, so a longer or punctuated code would fail validation rather than the flow.</item>
    /// </list>
    /// </remarks>
    internal async Task<SeededPrivateLeague> SeedPrivateLeagueToJoinAsync(string scope)
    {
        var email = $"joiner-{scope}@e2e.test".ToLowerInvariant();

        var playerUserId = await Seed.AddUserAsync("Jo", scope, email, E2ESettings.PlayerPassword);
        var administratorUserId = await Seed.AddUserAsync("Owner", scope);

        var competitionId = await Seed.AddCompetitionAsync($"J{Math.Abs(scope.GetHashCode()) % 100000}");
        var seasonId = await Seed.AddSeasonAsync(competitionId, $"{scope} Join Season");

        var entryCode = EntryCodeFor(scope);

        // The entry deadline must be in the FUTURE, and this is the opposite of what the leaderboard fixture
        // needs from the very same column - which is worth stating, because copying that one cost a CI run.
        //
        // LeagueEntry.IsOpen returns false for a null deadline: "a league with no entry deadline was silently
        // never joinable", a rule that used to be enforced only by SQL's NULL > anything being unknown. The
        // dashboard's private-league prompt is gated on IsOpen, so a null deadline here means no button to
        // click. The leaderboard journey needs the reverse - null or past, or the league page renders a
        // countdown instead of its leaderboards.
        var leagueId = await Seed.AddLeagueAsync(
            seasonId,
            administratorUserId,
            $"{scope} Private League",
            entryDeadlineUtc: DateTime.UtcNow.AddDays(7),
            entryCode: entryCode);

        // The pass, but deliberately NO AddLeagueMemberAsync - joining is the thing under test.
        await Seed.AddSeasonPassAsync(playerUserId, seasonId, source: SeasonPassSource.Free);

        return new SeededPrivateLeague(seasonId, leagueId, entryCode, email, E2ESettings.PlayerPassword);
    }

    /// <summary>
    /// Six characters of <c>[A-Z0-9]</c>, derived from the test class name so it is stable across runs and
    /// distinct between classes. The shape is not cosmetic - the validator rejects anything else.
    /// </summary>
    private static string EntryCodeFor(string scope)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        var hash = Math.Abs(scope.GetHashCode());
        var code = new char[6];

        for (var i = 0; i < code.Length; i++)
        {
            code[i] = alphabet[hash % alphabet.Length];
            hash /= alphabet.Length;
        }

        return new string(code);
    }
}
