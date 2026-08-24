# 0019. The query side reads at READ UNCOMMITTED, and write transactions hold only writes

- **Status:** Accepted
- **Date:** 2026-08-24
- **Deciders:** Antony Willson
- **Tags:** technical

## Context

Production logged a slow-query warning for the dashboard leaderboard read: **615ms, of which 0ms was
spent acquiring a connection and 0 work items were queued on the thread pool.** All 615ms was
server-side. The same query, run against representative data, takes no measurable time - the tables
are small (a few thousand rows), `IX_LeagueRoundResults_League_User` covers it exactly, and
`LeagueMembers` is keyed on `(LeagueId, UserId)`. There was no index to add and no plan to fix. The
read was waiting.

Three facts explain what it was waiting for.

**This instance cannot have `READ_COMMITTED_SNAPSHOT` enabled.** It is Fasthosts shared hosting
(`mssql04.mssql.prositehosting.net`), where the login has database-owner rights but the setting is
not ours to change. Without a snapshot to fall back on, a reader under `READ COMMITTED` takes shared
locks and waits for whichever writer holds the rows it wants. There is no third option.

**The write path holds those rows for a long time.** `UpdateMatchResultsCommand` was one
transaction covering everything the per-minute live-scores job does: prediction outcomes, round
tallies, per-league points, boosts, the round's status, the cached ranks - and then, on the tick that
finished a round, prize settlement, badge evaluation and **two rounds of outbound email**, one per
player, over HTTP to Brevo. Every write in it goes through Dapper's enumerable-parameter form, which
executes one round trip per row rather than one statement for the set, so a single tick makes on the
order of 130 sequential round trips to a database on another machine. The `LeagueRoundResults`
exclusive locks are taken partway through that and held until the commit.

**The reads that collide with it are the busiest on the site.** The dashboard polls its leaderboard
and My Leagues tiles every ten seconds for every open browser while a match is live, and the
leaderboard read touched nearly every row of `LeagueRoundResults` - it was not scoped to a season,
and it returned one row per (member, round) so the handler could add them up.

Two tiles had already met this and been patched individually: `MyLeaguesQuery` and
`LeagueRecordsQuery` each carried a per-query `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED`
wrapper with the reason recorded against it. The leaderboard tile reads the same table at the same
cadence and never got one, which is how a solved problem resurfaced in a new file.

Measured against the live server while investigating, and worth recording because it constrains the
implementation: **the isolation level survives `SqlConnection.Close()`.** Set it, close the
connection, reopen - the pool hands back the same SPID still holding the level. `sp_reset_connection`
does not clear it here. Closing a reader early does *not* skip a trailing reset in the same batch
(the remaining statements still run), but a read that *fails* mid-batch does.

## Decision

**1. Every query-side read runs at `READ UNCOMMITTED`, decided in one place.**
`IReadIsolationPolicy` wraps each read's SQL; `ReadUncommittedIsolationPolicy` sets the level, runs
the read, and sets it back in the same batch. `DapperReadDbConnection` applies it to every call, so
the answer to "what level do reads run at" is one registration rather than one decision per query
file. The two per-query wrappers are deleted.

This applies to `IApplicationReadDbConnection` only - the query side. Repository reads on the command
path are untouched, because those feed writes and must not decide on data that may not exist. The
CQRS split in `CLAUDE.md` is what makes that boundary sharp enough to rely on.

**2. Write transactions are pinned to `READ COMMITTED` explicitly.** `DbTransactionContext` passes
`IsolationLevel.ReadCommitted` to `BeginTransaction` rather than inheriting the session's level. A
read that failed before restoring the level would otherwise hand the next command a connection that
reads dirty. This costs nothing - the provider sends it with `BEGIN TRANSACTION`.

**3. Non-database work leaves the write transaction.** `UpdateMatchResultsCommand` becomes a
sequence of two commands: `ScoreMatchResultsCommand`, which is transactional and contains only the
writes, and `CompleteRoundCommand`, which settles prizes and badges and sends the emails, sent only
after the first has committed. `CommitAsync` now ends the transaction and releases the connection,
which is what makes "after the commit" expressible at all - it previously left
`HasActiveTransaction` true for the rest of the request scope, so any repository call after a commit
was handed a committed transaction object.

**4. The leaderboard total is summed by the database.** One row per (league, member) instead of one
per (member, round, season). The rows scanned are unchanged; what crosses the wire, every ten
seconds, per open dashboard, is not.

## Consequences

**For / positive**
- A read can no longer be blocked by a writer, which was the entire 615ms. It applies to every query
  on the site, not the three tiles that happened to be noticed.
- The window during which a writer holds locks on the busiest table shrinks from "until the emails
  finish" to "until the writes finish".
- Emails for a round are no longer sent from inside a transaction that could still roll back.
- One place to look, and one place to change, if this turns out to be wrong.

**Against / cost**
- **Dirty reads are now possible everywhere on the query side.** A total can briefly include a write
  that is rolled back, and an allocation-order scan can miss or double-count a row while pages move.
  Nothing on this path decides what to write, and the tiles that show these numbers re-poll every ten
  seconds, so a wrong number corrects itself - but it can be seen. The reads that would be worst to
  be wrong about are the money ones (payouts, prize winners); they are display-only and settled from
  the write path, so a stale figure there is cosmetic rather than consequential.
- Round settlement is no longer atomic with the scoring that triggered it. Every step in it is
  idempotent, and two of them are already reachable from admin actions that run untransacted, so a
  failure part-way is recoverable by re-running - but it is a genuine reduction in atomicity, taken
  deliberately.
- Three commands where there was one.

**Neutral / notes**
- If `READ_COMMITTED_SNAPSHOT` ever becomes available, this decision should be revisited: RCSI gives
  the same freedom from blocking without the dirty reads, and would make the policy a no-op.
- The row-by-row writes are *not* fixed here. Collapsing ~130 sequential round trips per tick into a
  handful of set-based statements is the other half of the same problem and remains outstanding; the
  slow-transaction warning added alongside the split timings is what will show whether it still
  matters once the settlement work is out of the way.
- `sp_describe_first_result_set` sees the unwrapped SQL, because the wrapping happens at runtime, so
  `tools/ThePredictions.SchemaCheck` is unaffected.

## Alternatives considered

- **Enable `READ_COMMITTED_SNAPSHOT`.** The right answer, and not available on this hosting.
- **Add an index.** The natural reading of a slow-query warning, and wrong here: the read already has
  a covering index and costs no measurable server time. This is what the split timings added in the
  previous change exist to rule out.
- **Keep patching individual queries with the hint.** What was already happening, and how the same
  problem reached a third file. Two copies of a decision are two chances for the third caller to miss
  it.
- **`WITH (NOLOCK)` table hints.** The same semantics spelled out per table, on every table, in every
  query - strictly more to write and more to forget.
- **Set the level on the connection after opening it.** Clean, and doubles the round trips per read.
  Prepending it to the batch costs nothing.
- **Open a `ReadUncommitted` transaction per read.** Two extra round trips (begin, rollback) to avoid
  string concatenation.
- **A post-commit callback queue on `IDbTransactionContext`.** Would keep the settlement work in one
  handler, at the price of running it outside the MediatR pipeline with no clear failure semantics.
  Two commands and an explicit sequence say the same thing in the vocabulary already here.

## Related

- [0015](./0015-cache-my-leagues-ranks.md) - the same tile, the same write path, the compile cost
  rather than the lock wait.
- [0017](./0017-sql-belongs-to-the-persistence-adapter.md) - why the isolation level is an adapter
  concern: it describes how one engine takes locks and means nothing to an adapter that takes none.
- [0018](./0018-log-severity-says-who-must-act.md) - why a slow query is a Warning, which is how this
  one was seen at all.
- [`docs/guides/database.md`](../guides/database.md)
