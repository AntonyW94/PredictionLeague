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
    throw new InvalidOperationException("Round status has changed");
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

For the complete schema, see [`docs/guides/database/database-schema.md`](../guides/database/database-schema.md).
