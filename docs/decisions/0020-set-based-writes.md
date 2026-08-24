# 0020. Writes that store many rows send them as JSON, not one round trip each

- **Status:** Accepted
- **Date:** 2026-08-24
- **Deciders:** Antony Willson
- **Tags:** technical

## Context

Dapper executes a command **once per element** when the object passed as its parameter is an
`IEnumerable`. It is a genuinely useful feature and it reads exactly like a batch:

```csharp
var command = new CommandDefinition(sql, matches.Select(m => new { m.Id, m.Status }), transaction: Transaction);
await Connection.ExecuteAsync(command);
```

That is one statement per match, sequentially, on the same connection. Twelve statements in this codebase
were written that way, including every write on the per-minute scoring path. A single tick made on the order
of **130 sequential round trips** to a database on a different machine: prediction outcomes, round tallies,
per-league points, boosts. The player-facing prediction submit did the same - one round trip per fixture
while somebody waited at the deadline.

Latency is the smaller half of the cost. With `READ_COMMITTED_SNAPSHOT` unavailable on this instance
(ADR-0019), a reader waits for whichever writer holds the rows it wants, so the duration a write transaction
stays open is the duration unrelated reads can be blocked for. Every extra round trip inside the transaction
extends that. ADR-0019 measured the other end of it: a dashboard read logged at 615ms of pure waiting, for a
query that costs no measurable server time to run.

## Decision

**Every write that stores more than one row sends them as one JSON parameter and reads them back with
`OPENJSON(@Rows) WITH (...)`.** `JsonRows.From(rows)` builds the parameter; each statement declares its own
columns and their types in the `WITH` clause, next to the SQL that uses them.

Three details are load-bearing rather than incidental:

**The parameter is pinned to `nvarchar(max)`.** Left to Dapper, a string parameter is sized from its content -
`nvarchar(4000)` for a small batch, `nvarchar(max)` for a large one. That is two parameter signatures and two
cached plans for one statement. ADR-0015 exists because a recompile on this instance cost roughly 400ms, so
plan-cache churn is a live concern here rather than a theoretical one.

**Every JSON path is `strict`.** `OPENJSON` is lax by default: a path naming a property the JSON does not
carry yields NULL rather than an error, so one typo in a `WITH` clause silently writes NULL over a column - no
exception, and for a nullable column no failure of any kind. Verified against the live server: `strict` raises
*"Property cannot be found on the specified JSON path"* instead, and still accepts a property that is present
and null, which is what the serialiser emits for a null value. `SetBasedWriteConventionTests` requires it, and
requires each path to name the column it feeds.

**An enum bound for an `int` column is cast in C#.** `[UserPredictions].[Outcome]` stores the enum's
underlying value, and what a serialiser does with an enum by default is not something the stored value should
depend on.

## Consequences

**For / positive**
- One round trip per write instead of one per row: ~130 per scoring tick becomes about a dozen.
- The write transaction is open for correspondingly less time, and with no snapshot isolation available that
  is the same thing as unrelated reads waiting less.
- The deadline-rush prediction submit is one statement rather than one per fixture.
- The column types are stated in the `WITH` clause, so what the statement expects is readable next to it -
  more legible than the previous form, where the types were whatever Dapper inferred from an anonymous object.
- Matching is by property name, so a row object may list its properties in any order. Unlike the read side's
  positional records, reordering cannot break a write.

**Against / cost**
- A layer of indirection: the row objects are now serialised rather than handed to Dapper directly, and a
  mistake in the `WITH` clause is a runtime failure rather than a compile error. `strict` mode, the convention
  tests and the conformance tests are the three answers to that, in that order.
- JSON is a slightly odd transport for a set of rows. It is the least-bad option here (see below).
- Writing 70 column/type declarations by hand duplicates what the schema already knows. Each was checked
  against `sys.columns` on the live database rather than against the schema doc, and two were wrong on the
  first pass - `Matches.Status` is `nvarchar(50)` and `ApiRoundName` is `nvarchar(128)`, both of which would
  have silently truncated.

**Neutral / notes**
- `UserBadgeRepository.AwardAsync` is deliberately left alone: it awards one badge and returns whether it
  inserted, so it is a per-row API by design rather than a batch written badly. Its caller loops, and making
  it set based would mean returning which of the batch were new.
- Verification, in order of strength: the twelve statements were compiled against the live schema under
  `SET NOEXEC ON` (which binds every column and would reject a wrong name or an unconvertible type); the
  conformance suite exercises the scoring writes and the prediction writes against real SQL Server in
  Testcontainers; the convention tests pin `strict` and the path/column agreement; `JsonRowsTests` pins the
  JSON each type produces.

## Alternatives considered

- **Table-valued parameters.** The textbook answer, and the one to revisit if a statement ever outgrows JSON.
  Rejected for now because each shape needs a user-defined table type in the database: a migration per shape,
  a schema object to keep in step with every column change, and another thing for
  `docs/guides/database-schema.md` and the refresh tool to track. JSON needs none of that.
- **A multi-row `VALUES` list with generated parameter names.** One parameter per column per row, so the SQL
  text - and therefore the cached plan - changes with the row count, and 2100 parameters is a hard ceiling.
  Given ADR-0015, deliberately churning the plan cache is the wrong direction.
- **`SqlBulkCopy`.** Right for tens of thousands of rows and wrong for forty: it cannot upsert, so the
  `MERGE`s would need a staging table each.
- **Leaving it alone and relying on ADR-0019.** Reads no longer block behind writers, so the pressure is off -
  but the transaction is still open for as long as the round trips take, which is also how long a *writer*
  waits, and the prediction submit is a user waiting on it directly.

## Related

- [0019](./0019-read-uncommitted-for-query-side-reads.md) - the reading half of the same problem, and the
  615ms measurement that started it.
- [0015](./0015-cache-my-leagues-ranks.md) - why plan-cache churn is treated as a real cost here.
- [0017](./0017-sql-belongs-to-the-persistence-adapter.md) - why these statements are all in one project.
- [`docs/guides/database.md`](../guides/database.md)
