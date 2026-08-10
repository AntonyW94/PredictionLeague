using System.Reflection;

namespace ThePredictions.Persistence.SqlServer.Migrations;

/// <summary>
/// The identity of the committed DbUp migration set: which assembly carries the scripts, and which table
/// records what has been applied.
///
/// This exists because DbUp keys its journal on the <b>manifest resource name</b> of each script, so
/// those names are the primary keys in every database's <c>dbo.SchemaVersions</c> table - production, dev
/// and backup included. Renaming this project, renaming this folder, or moving these files renames the
/// keys, and DbUp then sees seven unapplied scripts and re-runs them. Defining the identity here rather
/// than separately in each consumer means there is one answer, and
/// <c>MigrationScriptsTests</c> pins the resulting names so the consequence is a failing build
/// rather than a surprise migration run.
/// </summary>
public static class MigrationScripts
{
    /// <summary>The assembly whose embedded resources are the migration set.</summary>
    public static Assembly Assembly => typeof(MigrationScripts).Assembly;

    /// <summary>Schema of the DbUp journal table.</summary>
    public const string JournalSchema = "dbo";

    /// <summary>Name of the DbUp journal table.</summary>
    public const string JournalTable = "SchemaVersions";

    /// <summary>
    /// Every migration script's DbUp name - the manifest resource name, and therefore the value written to
    /// <see cref="JournalTable"/> - in the order DbUp applies them. Ordinal sort, which the zero-padded
    /// <c>NNNN_</c> prefix makes equivalent to numeric order.
    /// </summary>
    public static IReadOnlyList<string> Names() =>
        Assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
}
