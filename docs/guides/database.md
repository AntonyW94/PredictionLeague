# Database Conventions

This project uses SQL Server with Dapper for data access. Follow these conventions for all database operations.

## SQL Naming Conventions

**ALWAYS use brackets around table and column names in PascalCase. ALWAYS use table aliases (without brackets). ALWAYS put one column per line in SELECT, INSERT and UPDATE statements. Each SQL keyword (SELECT, FROM, WHERE, AND, ORDER BY, GROUP BY, JOIN, SET, VALUES, etc.) goes on its own line, with the next line indented.**

```sql
-- CORRECT: brackets, alias, one column per line, keywords on own lines
SELECT
    l.[Id],
    l.[Name],
    l.[CreatedAtUtc]
FROM
    [Leagues] l
WHERE
    l.[SeasonId] = @SeasonId
    AND l.[Status] = @Status
ORDER BY
    l.[Name]

-- WRONG - no brackets, no alias, columns on one line, wrong casing
SELECT Id, name, created_at_utc FROM Leagues WHERE season_id = @SeasonId
```

## Parameterised Queries

**ALWAYS use parameterised queries. NEVER concatenate user input into SQL.**

```csharp
// CORRECT - parameterised
const string sql = @"
    SELECT
        l.[Id],
        l.[Name]
    FROM
        [Leagues] l
    WHERE
        l.[Id] = @Id";
await _connection.QueryAsync<League>(sql, new { Id = leagueId });

// WRONG - SQL injection vulnerability
var sql = $"SELECT * FROM [Leagues] WHERE [Id] = {leagueId}";
await _connection.QueryAsync<League>(sql);
```

## Result Mapping

**Query handlers materialise into private result records, never directly into Contracts DTOs.**

Dapper matches a positional `record`'s constructor to the result set **positionally** - parameter *N* must line up with `SELECT` column *N* by both name and type. A mismatch is not caught by the compiler or unit tests; it throws at runtime (`InvalidOperationException: A parameterless default constructor or one matching signature (...) is required`).

- The generic argument to `QueryAsync<T>` / `QuerySingleOrDefaultAsync<T>` must be a `private record XxxQueryResult(...)` co-located in the handler (or another Application-owned row type), kept in lockstep with the `SELECT` column order.
- Map from the result record to the outward Contracts DTO **by name** afterwards (explicit constructor call or object initialiser), so a Contracts reshape becomes a compile error in the handler.
- Scalars (`int`, `bool`, `string`) and named tuples private to the handler are exempt.
- Computed/`CASE`/`COALESCE` columns must be aliased and counted as a column in the ordering, and the record parameter typed to match the column.
- **Boolean-shaped `CASE` expressions must be cast to `bit` in the SQL:** `CAST(CASE WHEN ... THEN 1 ELSE 0 END AS bit) AS IsFinished`. An uncast `CASE ... THEN 1 ELSE 0 END` is an `int`, which forces an `int` result-record parameter and an `== 1` conversion in C# - and lets the int flag leak out through the DTO to the UI. Model booleans as booleans from the SQL outwards.

This keeps the fragile positional coupling inside a single file, next to its SQL, instead of extending it into the shared Contracts assembly where a UI-motivated constructor reorder would break queries at runtime.

### Types must match the column exactly, not the DTO

A positional record parameter must be the **exact** CLR type of the column (`Nullable<T>` aside). Dapper widens nothing: an `int` column will not fill a `long` parameter, and a `long` column will not fill an `int` one. This differs from the classes with `init` properties that Dapper maps **by name**, where it converts, so copying a type across from the outward DTO is exactly how this goes wrong:

```csharp
// [LeagueMemberStats].[SnapshotOverallRank] is an int column; LeaderboardEntryDto.SnapshotRank is long?
private record OverallLeaderboardQueryResult(..., long? SnapshotRank, ...);  // WRONG - throws at runtime
private record OverallLeaderboardQueryResult(..., int? SnapshotRank, ...);   // CORRECT - int? to long? widens in the C# mapping step
```

Type the parameter from the **column**, then let the C# mapping step widen into the DTO. Note that `RANK()`, `ROW_NUMBER()` and `COUNT_BIG()` return `bigint` (`long`), while `COUNT()` and `SUM()` over an `int` column return `int` - two rank-shaped columns in the same `SELECT` can legitimately need different types.

To check a query's real column types without running the app, ask SQL Server directly:

```sql
EXEC sys.sp_describe_first_result_set
    @tsql = N'<the query>',
    @params = N'@LeagueId int, @ApprovedStatus nvarchar(50)';
```

`system_type_name` in the output is what Dapper will see. Note that SQLite-backed tests would **not** catch this class of bug, because its type affinities differ from SQL Server's.

To check every read in one go rather than one query at a time, run [`tools/ThePredictions.SchemaCheck`](../../tools/ThePredictions.SchemaCheck/README.md):

```bash
dotnet run --project tools/ThePredictions.SchemaCheck
```

It compares each Dapper read in `src/` with the result set SQL Server describes for it, exits non-zero when a read cannot materialise, and also reports the quieter name-mapped failures below. Run it after changing any query's `SELECT` list or result type.

To have it run automatically over the reads your commits touch, enable the repository's hooks once per clone (git config is not versioned, so it does not arrive with a fresh clone):

```bash
git config core.hooksPath .githooks
```

`.githooks/pre-push` then checks the commits being pushed. It skips when they touch no `src` C# file or the database is unreachable, so it only ever blocks a push on a real finding - bypass one with `git push --no-verify`.

### Read failures are server faults, not client errors

Dapper reports a result-set/result-record mismatch as a plain `InvalidOperationException`. `DapperReadDbConnection` translates any `InvalidOperationException` out of a read into `ReadQueryFailedException`, which names the failure in the log message instead of leaving a bare Dapper exception to be interpreted.

Both types are reported as a 500 and an Error. Business rules throw `BusinessRuleViolationException` (400 and a Warning), so a materialisation bug cannot be mistaken for a client mistake - see [ADR-0016](../decisions/0016-business-rule-exception-classification.md). This used to be the other way round: `InvalidOperationException` meant "client error" and every infrastructure path had to remember to opt out, which is how a broken leaderboard spent a day logged as a Warning behind a 400.

## Isolation levels

**Every query-side read runs at `READ UNCOMMITTED`. You do not write that anywhere - it is applied once, for all of them.**

`DapperReadDbConnection` passes each read's SQL through `IReadIsolationPolicy` before executing it. The production implementation sets the level, runs the read, and sets it back in the same batch, so there is no extra round trip and nothing to remember per query. Do **not** add `SET TRANSACTION ISOLATION LEVEL` or `WITH (NOLOCK)` to a query - two files used to carry the hint by hand and that is exactly how a third one came to be missing it.

Why: this instance cannot have `READ_COMMITTED_SNAPSHOT` enabled, so a reader under `READ COMMITTED` has no snapshot to fall back on and simply waits for whichever writer holds the rows it wants. The cost is dirty reads, which is a real trade and is argued out in [ADR-0019](../decisions/0019-read-uncommitted-for-query-side-reads.md).

Two consequences worth knowing:

- **Repository reads on the command path are not affected**, and must not be. They feed writes, and a write decided from data that may not exist is a different class of bug from a leaderboard that is briefly wrong. This is one of the things the [CQRS split](cqrs-patterns.md) buys.
- **The isolation level rides pooled connections.** Measured against the live server: set it, close the connection, and the pool hands back the same SPID still holding the level - `sp_reset_connection` does not clear it. That is why `DbTransactionContext` passes `IsolationLevel.ReadCommitted` to `BeginTransaction` explicitly rather than inheriting whatever the session was left on.

## Transactions hold writes, and nothing else

`TransactionBehaviour` opens a transaction for any command marked `ITransactionalRequest` and commits it when the handler returns, so **the handler's whole body is the lock-hold window** - and with no snapshot isolation available, that window is also how long an unrelated read of the same rows can be made to wait.

So a transactional command must contain writes and the reads that inform them, and nothing slow that is not a write. HTTP calls, email sends, payment calls and anything else that can block on a third party belong in a separate, untransacted command sent **after** the transactional one returns. `ScoreMatchResultsCommand` / `CompleteRoundCommand` is the worked example: settling a round used to send a season's worth of email from inside the transaction that had just written the round's points.

`CommitAsync` ends the transaction and releases the connection, so a command sent after a commit gets a fresh autocommit connection rather than the committed transaction object.
## Set-based writes

**A write that stores more than one row sends them as one JSON parameter. Never pass Dapper a collection as a command's parameter.**

Dapper executes the command **once per element** when its parameter object is an `IEnumerable`. It reads exactly like a batch and is not one - it is N sequential round trips, all inside the transaction holding the write locks:

```csharp
// WRONG - one statement per match, sequentially
var command = new CommandDefinition(sql, matches.Select(m => new { m.Id, m.Status }), transaction: Transaction);

// CORRECT - one statement, one round trip
var rows = matches.Select(m => new { m.Id, Status = m.Status.ToString() }).ToList();
if (rows.Count == 0)
    return;

var command = new CommandDefinition(sql, new { Rows = JsonRows.From(rows) }, transaction: Transaction);
```

with the statement reading the rows back:

```sql
UPDATE
    m
SET
    m.[Status] = src.[Status]
FROM
    [Matches] m
INNER JOIN
    OPENJSON(@Rows)
    WITH (
        [Id] int 'strict $.Id',
        [Status] nvarchar(4000) 'strict $.Status'
    ) src ON src.[Id] = m.[Id];
```

Four rules, each enforced by `SetBasedWriteConventionTests` or explained by a failure it prevents:

| Rule | Why |
|------|-----|
| **Always `strict $.Column`** | `OPENJSON` is lax by default: a path naming a property the JSON does not carry yields NULL rather than an error, so one typo silently writes NULL over a column. `strict` raises "Property cannot be found on the specified JSON path" instead, and still accepts a property that is present and null. |
| **The JSON path names the column it feeds** | The row objects are built next to the statement, so the pair should read as one name twice. Anything else is a typo or a half-finished rename. |
| **Never declare a width** - `nvarchar(4000)` for text, `decimal(38, 10)` for money, and `int`/`bit`/`datetime2` as they are | The `WITH` clause is a **cast**, so a declared width narrower than the column **silently truncates** - measured: `nvarchar(20)` reading a 28-character value stored 20 and raised nothing, and `decimal(18,0)` reading `12.349` stored `12.00`. `strict` does not help; it only guards whether the property is there. Declaring wide makes the destination column the only place a width is stated, so an overflow becomes SQL Server's own "String or binary data would be truncated" at the insert. A wide `decimal` is rounded to the column's scale on the way in, which is the right answer rather than a lost one. |
| **Return early on an empty collection** | Nothing to store, so nothing to send. |

The width rule is the one worth arguing about, so: two of the first seventy declarations written here copied a width that was wrong, and neither the compiler, the unit tests, the conformance tests nor `strict` mode would have caught either. Not copying a width at all is what removes the error, rather than copying it more carefully.

Cast an enum bound for an `int` column (`Outcome = (int)prediction.Outcome`) rather than passing it whole - what a serialiser does with an enum by default is not something the stored value should depend on. An enum bound for a text column keeps its `.ToString()`.

Matching is by property name, so unlike the read side's positional records, the order the row object lists its properties in does not matter.

See [ADR-0020](../decisions/0020-set-based-writes.md) for why JSON rather than table-valued parameters or a multi-row `VALUES` list, and [ADR-0019](../decisions/0019-read-uncommitted-for-query-side-reads.md) for why the length of a write transaction is what unrelated reads pay for.

## DateTime Handling

**All dates are stored and retrieved in UTC.**

- Column names use `Utc` suffix: `CreatedAtUtc`, `DeadlineUtc`, `UpdatedAtUtc`
- The `DapperUtcDateTimeHandler` automatically sets `DateTimeKind.Utc` on deserialization
- Always use `DateTime.UtcNow` when inserting timestamps

```csharp
// CORRECT
const string sql = @"
    INSERT INTO [Leagues] (
        [Name],
        [CreatedAtUtc]
    )
    VALUES (
        @Name,
        @CreatedAtUtc
    )";

await _connection.ExecuteAsync(sql, new
{
    Name = league.Name,
    CreatedAtUtc = DateTime.UtcNow  // Always UTC
});

// WRONG
await _connection.ExecuteAsync(sql, new
{
    Name = league.Name,
    CreatedAtUtc = DateTime.Now  // NEVER use DateTime.Now
});
```

## Common Query Patterns

### Single Entity Lookup

```csharp
public async Task<League?> GetByIdAsync(int id, CancellationToken ct)
{
    const string sql = @"
        SELECT
            l.[Id],
            l.[Name],
            l.[SeasonId],
            l.[AdministratorUserId],
            l.[EntryCode],
            l.[CreatedAtUtc]
        FROM
            [Leagues] l
        WHERE
            l.[Id] = @Id";

    return await _connection.QuerySingleOrDefaultAsync<League>(sql, new { Id = id }, ct);
}
```

### List with Filtering

```csharp
public async Task<IEnumerable<LeagueDto>> GetBySeasonAsync(int seasonId, CancellationToken ct)
{
    const string sql = @"
        SELECT
            l.[Id],
            l.[Name],
            l.[CreatedAtUtc]
        FROM
            [Leagues] l
        WHERE
            l.[SeasonId] = @SeasonId
        ORDER BY
            l.[Name]";

    return await _connection.QueryAsync<LeagueDto>(sql, new { SeasonId = seasonId }, ct);
}
```

### Complex Joins with CTEs

Use CTEs for complex aggregations:

```csharp
const string sql = @"
    WITH [MemberCounts] AS (
        SELECT
            lm.[LeagueId],
            COUNT(*) AS [MemberCount]
        FROM
            [LeagueMembers] lm
        WHERE
            lm.[IsApproved] = 1
        GROUP BY
            lm.[LeagueId]
    )
    SELECT
        l.[Id],
        l.[Name],
        COALESCE(mc.[MemberCount], 0) AS [MemberCount]
    FROM
        [Leagues] l
    LEFT JOIN [MemberCounts] mc
        ON l.[Id] = mc.[LeagueId]
    WHERE
        l.[SeasonId] = @SeasonId
    ORDER BY
        l.[Name]";
```

### Insert with OUTPUT

Get the generated ID immediately:

```csharp
const string sql = @"
    INSERT INTO [Leagues] (
        [Name],
        [SeasonId],
        [CreatedAtUtc]
    )
    OUTPUT INSERTED.[Id]
    VALUES (
        @Name,
        @SeasonId,
        @CreatedAtUtc
    )";

var newId = await _connection.ExecuteScalarAsync<int>(sql, parameters);
```

### Conditional Updates

```csharp
const string sql = @"
    UPDATE r
    SET
        r.[Status] = @NewStatus,
        r.[UpdatedAtUtc] = @UpdatedAtUtc
    FROM
        [Rounds] r
    WHERE
        r.[Id] = @Id
        AND r.[Status] = @ExpectedStatus";

var rowsAffected = await _connection.ExecuteAsync(sql, new
{
    Id = roundId,
    NewStatus = RoundStatus.Published,
    ExpectedStatus = RoundStatus.Draft,
    UpdatedAtUtc = DateTime.UtcNow
});

if (rowsAffected == 0)
{
    throw new BusinessRuleViolationException("Round status has changed");
}
```

## Key Tables Reference

| Table | Purpose |
|-------|---------|
| `[Leagues]` | League definitions |
| `[LeagueMembers]` | User membership in leagues |
| `[LeaguePrizeSettings]` | Prize configuration per league |
| `[LeagueRoundResults]` | Cached scoring results per member per round |
| `[Rounds]` | Gameweeks/matchdays |
| `[Matches]` | Individual fixtures within rounds |
| `[UserPredictions]` | User predictions for matches |
| `[RoundResults]` | Aggregated round results per user |
| `[Winnings]` | Prize payouts |
| `[AspNetUsers]` | Identity users (extended with FirstName, LastName) |

For the complete schema, see [`docs/guides/database-schema.md`](database-schema.md).
