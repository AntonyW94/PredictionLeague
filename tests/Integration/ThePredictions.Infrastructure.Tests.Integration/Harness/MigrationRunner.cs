using System.Reflection;
using DbUp;
using DbUp.Builder;
using DbUp.Engine;

namespace ThePredictions.Infrastructure.Tests.Integration.Harness;

/// <summary>
/// Applies the committed DbUp migration set to the throwaway test database.
///
/// The schema is built by running the real migrations rather than a schema script kept alongside the
/// tests, for two reasons. A separate script drifts from the migrations silently, so the suite would
/// end up testing SQL against a shape production never had. And running them means every test run is
/// also proof that the migration set builds a working schema from empty - which nothing else checks,
/// because the three real databases were baselined from an existing schema and so have never applied
/// <c>0001_Baseline.sql</c> as anything but a no-op.
///
/// Deliberately mirrors <c>ThePredictions.DatabaseTools.DatabaseMigrator</c> (same journal table, same
/// transaction-per-script, same timeout) rather than referencing it: that project is an executable
/// pinned to a newer Microsoft.Data.SqlClient than the application, and a test that reached into it
/// would quietly test the tooling's driver instead of the application's.
/// </summary>
internal static class MigrationRunner
{
    // DDL (the guarded baseline, index and constraint creation) can take a while on a cold container.
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(5);

    internal const string JournalSchema = "dbo";
    internal const string JournalTable = "SchemaVersions";

    /// <summary>
    /// Runs every pending migration, throwing with the offending script named if any fails.
    /// </summary>
    internal static void Apply(string connectionString)
    {
        DatabaseUpgradeResult result = Build(connectionString).PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException(
                $"The committed migrations do not apply to an empty database. Failed on "
                + $"'{result.ErrorScript?.Name}': {result.Error?.Message}");
    }

    /// <summary>
    /// Whether the migrator still has scripts to run. False after <see cref="Apply"/> on a database
    /// whose journal lists every embedded script.
    /// </summary>
    internal static bool IsUpgradeRequired(string connectionString) =>
        Build(connectionString).IsUpgradeRequired();

    /// <summary>
    /// The migration scripts embedded in this assembly, by their DbUp script name - which is the
    /// resource name, and therefore what lands in the journal table.
    /// </summary>
    internal static IReadOnlyList<string> EmbeddedScriptNames() =>
        Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Where(n => n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static UpgradeEngine Build(string connectionString)
    {
        UpgradeEngineBuilder builder = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransactionPerScript()
            .WithExecutionTimeout(ScriptTimeout)
            .JournalToSqlTable(JournalSchema, JournalTable)
            // Silenced: a passing run has nothing to say, and the baseline alone logs hundreds of
            // lines. A failure is reported by Apply, which names the script and the error.
            .LogToNowhere();

        return builder.Build();
    }
}
