using ThePredictions.Application.Data;

namespace ThePredictions.Persistence.SqlServer.Data;

/// <summary>
/// Runs every query-side read at <c>READ UNCOMMITTED</c>, then puts the session back to
/// <c>READ COMMITTED</c> in the same batch.
/// </summary>
/// <remarks>
/// This managed instance cannot have <c>READ_COMMITTED_SNAPSHOT</c> enabled, so a reader has no snapshot to
/// fall back on: under <c>READ COMMITTED</c> it takes shared locks and waits for whichever writer holds the
/// rows it wants. The write path holds those locks for as long as its transaction is open, which is long
/// enough to be measured - the dashboard's leaderboard read was logged at 615ms of pure waiting for a query
/// that costs no measurable server time to run. Two tiles already carried this hint per-query for exactly
/// that reason; applying it at the one place every read passes through replaces the copies.
///
/// The trade is dirty reads: a total can briefly include a write that is later rolled back, and an
/// allocation-order scan can miss or double-count a row while pages are moving. Every read on this path is
/// query-side (see the CQRS split - commands write through repositories, which are not affected by this),
/// nothing on it decides what to write, and the live tiles that surface these numbers re-poll every ten
/// seconds, so a transient wrong number corrects itself. See ADR-0019.
///
/// The reset matters more than it looks. Measured against the live server: the isolation level survives
/// <c>SqlConnection.Close()</c> and is still in force when the pool hands the same SPID out again, so
/// without the reset the level would leak to whatever ran next. Closing the reader early does not skip it -
/// the level is back to READ COMMITTED whether or not the caller drains the trailing statements. A read
/// that fails mid-batch does skip it, which is why the write path pins its own level when it opens a
/// transaction rather than trusting the session it inherits.
/// </remarks>
public sealed class ReadUncommittedIsolationPolicy : IReadIsolationPolicy
{
    private const string ReadUncommitted = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;";
    private const string ReadCommitted = "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;";

    /// <summary>
    /// The terminator sits on its own line so the read's SQL is never edited to make the batch parse:
    /// queries end variously in a semicolon, in no semicolon at all, and in a trailing line comment that
    /// would swallow one appended to it. An empty statement is legal T-SQL, so the redundant case is free.
    /// </summary>
    public string Apply(string sql) => $"{ReadUncommitted}\n{sql}\n;\n{ReadCommitted}";
}
