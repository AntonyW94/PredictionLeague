using System.Text.Json;
using Dapper;

namespace ThePredictions.Persistence.SqlServer.Data;

/// <summary>
/// Carries a set of rows to SQL Server as one parameter, for statements that read them back with
/// <c>OPENJSON(@Rows) WITH (...)</c>.
/// </summary>
/// <remarks>
/// This exists because of what Dapper does with an <see cref="IEnumerable{T}"/> passed as a command's
/// parameter: it executes the statement once per element, sequentially, on the same connection. That reads
/// as a batch and is not one. A single scoring tick was making roughly 130 round trips to a database on
/// another machine - prediction outcomes, round tallies, league points, boosts - each holding the
/// transaction (and therefore its write locks) open a little longer, which is what an unrelated dashboard
/// read was measured waiting 615ms behind. See ADR-0019 for that measurement and ADR-0020 for this fix.
///
/// The alternatives, and why not:
///
///   - <b>Table-valued parameters</b> are the textbook answer and need a user-defined table type per shape,
///     which means a migration and a schema object to keep in step with every column change.
///   - <b>A multi-row <c>VALUES</c> list</b> needs one parameter per column per row, so the SQL text - and
///     therefore the cached plan - changes with the row count, and 2100 parameters is a hard ceiling.
///     Plan-cache churn is a live concern here rather than a theoretical one: ADR-0015 exists because a
///     recompile on this instance cost ~400ms.
///
/// JSON has neither problem: one parameter, one plan whatever the row count, and the column types stated in
/// the <c>WITH</c> clause right next to the statement that uses them.
/// </remarks>
internal static class JsonRows
{
    /// <summary>
    /// The rows as a single <c>nvarchar(max)</c> parameter.
    /// </summary>
    /// <remarks>
    /// Pinned to <c>nvarchar(max)</c> with an explicit length rather than left to Dapper, which sizes a
    /// string parameter from its content and would hand SQL Server <c>nvarchar(4000)</c> for a small batch
    /// and <c>nvarchar(max)</c> for a large one - two parameter signatures, two cached plans, for the same
    /// statement.
    ///
    /// Verified against the live server: property names are matched case-sensitively by the <c>WITH</c>
    /// clause's JSON paths, and every type this codebase stores survives the round trip - <c>int</c>,
    /// <c>nvarchar</c> (including <c>\uXXXX</c> escapes and surrogate pairs, which the default encoder
    /// produces), <c>money</c>, <c>bit</c> from <c>true</c>/<c>false</c>, JSON <c>null</c> as SQL NULL, and
    /// <c>datetime2(7)</c> at full precision from the ISO 8601 form with a <c>Z</c> suffix.
    /// </remarks>
    internal static DbString From<T>(IReadOnlyCollection<T> rows) =>
        new()
        {
            Value = JsonSerializer.Serialize(rows),
            IsAnsi = false,
            IsFixedLength = false,
            Length = -1
        };
}
