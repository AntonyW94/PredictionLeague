using DbUp;
using DbUp.Engine;
using ThePredictions.Persistence.SqlServer.Migrations;

namespace ThePredictions.DatabaseTools;

/// <summary>
/// Applies the embedded SQL migration scripts to a target database using DbUp.
/// DbUp records applied scripts per-database in a <c>SchemaVersions</c> table, so the same
/// migration set can be run safely against every database; each only applies what it has not
/// yet seen. Scripts run one-per-transaction with a generous timeout for DDL.
/// </summary>
public class DatabaseMigrator(string connectionString)
{
    // DDL (large guarded baseline, index/constraint creation) can take a while; give it room.
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Runs all pending migrations. Returns true on success, false on failure.
    /// Never logs the connection string.
    /// </summary>
    public bool Run()
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            // The scripts live in the persistence adapter, which owns the schema it speaks to. Reading
            // them from there rather than embedding a second copy here keeps one set of resource names,
            // and therefore one set of journal keys.
            .WithScriptsEmbeddedInAssembly(MigrationScripts.Assembly)
            .WithTransactionPerScript()
            .WithExecutionTimeout(ScriptTimeout)
            .JournalToSqlTable(MigrationScripts.JournalSchema, MigrationScripts.JournalTable)
            .LogToConsole()
            .Build();

        if (!upgrader.IsUpgradeRequired())
        {
            Console.WriteLine("[INFO] Database is up to date - no migrations to apply.");
            return true;
        }

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();

        if (result.Successful)
        {
            Console.WriteLine("[SUCCESS] Database migration completed.");
            return true;
        }

        // result.Error.Message can contain SQL detail but not the connection string.
        Console.WriteLine($"[ERROR] Migration failed on script '{result.ErrorScript?.Name}': {result.Error?.Message}");
        return false;
    }
}
