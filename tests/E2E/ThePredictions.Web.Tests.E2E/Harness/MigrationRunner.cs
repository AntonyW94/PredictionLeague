using DbUp;
using DbUp.Builder;
using DbUp.Engine;
using ThePredictions.Persistence.SqlServer.Migrations;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Applies the committed DbUp migration set to the throwaway database this run creates.
///
/// The schema is built by running the real migrations rather than a script kept beside the tests, for the
/// same two reasons the integration suite does it: a separate script drifts silently, so the suite would
/// end up driving an application against a shape production never had; and running them makes every run
/// proof that the migration set builds a working schema from empty.
///
/// The scripts and the journal identity come from <see cref="MigrationScripts"/> in the persistence
/// adapter, so this suite, the integration suite and <c>ThePredictions.DatabaseTools</c> cannot disagree
/// about either. Only the DbUp wiring below is written more than once; sharing that would mean putting a
/// <c>dbup-sqlserver</c> dependency in the adapter, which would ship a migration engine inside the web
/// application for no benefit.
/// </summary>
internal static class MigrationRunner
{
    // The guarded baseline plus index and constraint creation can take a while on a cold container.
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Runs every pending migration, throwing with the offending script named if any fails.</summary>
    internal static void Apply(string connectionString)
    {
        DatabaseUpgradeResult result = Build(connectionString).PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException(
                "The committed migrations do not apply to an empty database. Failed on "
                + $"'{result.ErrorScript?.Name}': {result.Error?.Message}");
    }

    private static UpgradeEngine Build(string connectionString)
    {
        UpgradeEngineBuilder builder = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(MigrationScripts.Assembly)
            .WithTransactionPerScript()
            .WithExecutionTimeout(ScriptTimeout)
            .JournalToSqlTable(MigrationScripts.JournalSchema, MigrationScripts.JournalTable)
            // Silenced: a passing run has nothing to say, and the baseline alone logs hundreds of lines.
            // A failure is reported by Apply, which names the script and the error.
            .LogToNowhere();

        return builder.Build();
    }
}
