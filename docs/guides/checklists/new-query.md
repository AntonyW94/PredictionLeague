# Checklist: Creating a New Query

Use this checklist when creating a new query (read operation).

## Files to Create

- [ ] `src/PredictionLeague.Application/Features/{Area}/Queries/{QueryName}/{QueryName}Query.cs`
- [ ] `src/PredictionLeague.Application/Features/{Area}/Queries/{QueryName}/{QueryName}QueryHandler.cs`
- [ ] `src/PredictionLeague.Contracts/{Area}/{ResultDto}.cs`

## Query Definition

```csharp
// {QueryName}Query.cs
public record {QueryName}Query(
    // Filter/input properties
    int LeagueId,
    string UserId) : IRequest<{ResultType}>;
```

## Handler Implementation

**CRITICAL: Queries use `IApplicationReadDbConnection`, NEVER repositories.**

```csharp
// {QueryName}QueryHandler.cs
public class {QueryName}QueryHandler : IRequestHandler<{QueryName}Query, IEnumerable<{ResultDto}>>
{
    private readonly IApplicationReadDbConnection _readDb;

    public {QueryName}QueryHandler(IApplicationReadDbConnection readDb)
    {
        _readDb = readDb;
    }

    public async Task<IEnumerable<{ResultDto}>> Handle({QueryName}Query request, CancellationToken ct)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                l.[CreatedAtUtc]
            FROM [Leagues] l
            INNER JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            WHERE lm.[UserId] = @UserId
            ORDER BY l.[Name]";

        return await _readDb.QueryAsync<{ResultDto}>(sql, new { request.UserId }, ct);
    }
}
```

## DTO Definition

```csharp
// {ResultDto}.cs
public record {ResultDto}(
    int Id,
    string Name,
    DateTime CreatedAtUtc);
```

## SQL Guidelines

- [ ] Use brackets around all table/column names: `[TableName]`, `[ColumnName]`
- [ ] Use PascalCase for SQL identifiers
- [ ] Use `@ParameterName` for all parameters (never concatenate)
- [ ] Use CTEs for complex aggregations
- [ ] Select only needed columns (no `SELECT *`)

```sql
-- Complex query with CTE example
WITH [MemberStats] AS (
    SELECT
        [LeagueId],
        COUNT(*) AS [MemberCount],
        SUM(CASE WHEN [IsApproved] = 1 THEN 1 ELSE 0 END) AS [ApprovedCount]
    FROM [LeagueMembers]
    GROUP BY [LeagueId]
)
SELECT
    l.[Id],
    l.[Name],
    COALESCE(ms.[MemberCount], 0) AS [MemberCount],
    COALESCE(ms.[ApprovedCount], 0) AS [ApprovedCount]
FROM [Leagues] l
LEFT JOIN [MemberStats] ms ON l.[Id] = ms.[LeagueId]
WHERE l.[SeasonId] = @SeasonId
```

## Verification Checklist

- [ ] Handler injects `IApplicationReadDbConnection` (NOT a repository)
- [ ] SQL uses brackets around all identifiers
- [ ] SQL uses parameterised queries
- [ ] Returns DTOs, not domain models
- [ ] One public type per file
- [ ] All async methods end with `Async`
- [ ] UK English spelling used throughout
- [ ] DateTime columns have `Utc` suffix

## Testing Checklist

- [ ] Unit tests written for any new domain logic triggered by the query
- [ ] Test naming follows `MethodName_ShouldX_WhenY()` convention
- [ ] Coverage report run (`tools\Test Coverage\coverage-unit.bat`) and confirms 100% line and branch coverage
- [ ] Any untestable code (e.g. ORM constructors) marked with `[ExcludeFromCodeCoverage]`

## Common Mistakes to Avoid

```csharp
// WRONG - using repository in query
public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<LeagueDto>>
{
    private readonly ILeagueRepository _leagueRepository; // DON'T DO THIS!
}

// WRONG - returning domain models
public async Task<IEnumerable<League>> Handle(...) // Should return DTOs
{
    return await _readDb.QueryAsync<League>(sql, params, ct);
}

// WRONG - SQL without brackets
const string sql = "SELECT Id, Name FROM Leagues"; // Missing brackets
```
