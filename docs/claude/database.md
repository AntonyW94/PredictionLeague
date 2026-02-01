# Database Conventions

This project uses SQL Server with Dapper for data access. Follow these conventions for all database operations.

## SQL Naming Conventions

**ALWAYS use brackets around table and column names in PascalCase.**

```sql
-- CORRECT
SELECT [Id], [Name], [CreatedAtUtc]
FROM [Leagues]
WHERE [SeasonId] = @SeasonId

-- WRONG - no brackets, wrong casing
SELECT Id, name, created_at_utc
FROM Leagues
WHERE season_id = @SeasonId
```

## Parameterised Queries

**ALWAYS use parameterised queries. NEVER concatenate user input into SQL.**

```csharp
// CORRECT - parameterised
const string sql = "SELECT * FROM [Leagues] WHERE [Id] = @Id";
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
    INSERT INTO [Leagues] ([Name], [CreatedAtUtc])
    VALUES (@Name, @CreatedAtUtc)";

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
        SELECT [Id], [Name], [SeasonId], [AdministratorUserId], [EntryCode], [CreatedAtUtc]
        FROM [Leagues]
        WHERE [Id] = @Id";

    return await _connection.QuerySingleOrDefaultAsync<League>(sql, new { Id = id }, ct);
}
```

### List with Filtering

```csharp
public async Task<IEnumerable<LeagueDto>> GetBySeasonAsync(int seasonId, CancellationToken ct)
{
    const string sql = @"
        SELECT [Id], [Name], [CreatedAtUtc]
        FROM [Leagues]
        WHERE [SeasonId] = @SeasonId
        ORDER BY [Name]";

    return await _connection.QueryAsync<LeagueDto>(sql, new { SeasonId = seasonId }, ct);
}
```

### Complex Joins with CTEs

Use CTEs for complex aggregations:

```csharp
const string sql = @"
    WITH [MemberCounts] AS (
        SELECT
            [LeagueId],
            COUNT(*) AS [MemberCount]
        FROM [LeagueMembers]
        WHERE [IsApproved] = 1
        GROUP BY [LeagueId]
    )
    SELECT
        l.[Id],
        l.[Name],
        COALESCE(mc.[MemberCount], 0) AS [MemberCount]
    FROM [Leagues] l
    LEFT JOIN [MemberCounts] mc ON l.[Id] = mc.[LeagueId]
    WHERE l.[SeasonId] = @SeasonId
    ORDER BY l.[Name]";
```

### Insert with OUTPUT

Get the generated ID immediately:

```csharp
const string sql = @"
    INSERT INTO [Leagues] ([Name], [SeasonId], [CreatedAtUtc])
    OUTPUT INSERTED.[Id]
    VALUES (@Name, @SeasonId, @CreatedAtUtc)";

var newId = await _connection.ExecuteScalarAsync<int>(sql, parameters);
```

### Conditional Updates

```csharp
const string sql = @"
    UPDATE [Rounds]
    SET [Status] = @NewStatus, [UpdatedAtUtc] = @UpdatedAtUtc
    WHERE [Id] = @Id AND [Status] = @ExpectedStatus";

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

For the complete schema, see [`/docs/database-schema.md`](../database-schema.md).
