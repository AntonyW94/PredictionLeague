# Fix Plan: Boost System Race Condition

> **MANUAL SQL STEP REQUIRED**: This plan contains database schema changes that must be run manually. When implementing this fix, stop at Step 1 and provide the SQL to execute before proceeding with code changes.

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P0 - Critical |
| Severity | Critical |
| Type | Race Condition / Business Logic |
| CWE | CWE-362: Concurrent Execution Using Shared Resource with Improper Synchronization |
| OWASP | Business Logic Flaws |

---

## Vulnerability Details

### Description
The UserBoostUsages table lacks a unique constraint on (UserId, LeagueId, RoundId, BoostDefinitionId), allowing users to apply the same boost multiple times to a single round through concurrent requests.

### Affected Files
- `PredictionLeague.Infrastructure/Repositories/Boosts/BoostWriteRepository.cs` (lines 12-46)
- Database schema: `UserBoostUsages` table

### Attack Vector
```
Timeline:
T1: User sends ApplyBoost request #1 (eligibility check passes)
T2: User sends ApplyBoost request #2 (eligibility check passes - DB unchanged)
T3: Request #1 inserts UserBoostUsage record
T4: Request #2 inserts SECOND UserBoostUsage record (no constraint violation)
Result: User has 2 boost records, achieving 4x points instead of 2x
```

### Impact
- Users can multiply their boost effects infinitely
- Prize distribution corrupted
- Unfair competitive advantage
- Financial impact on prize payouts

---

## Fix Implementation

### Step 1: Add Database Unique Constraint

**Migration SQL:**
```sql
-- Add unique constraint to prevent duplicate boost applications
ALTER TABLE [UserBoostUsages]
ADD CONSTRAINT UK_UserBoostUsages_UserLeagueRoundBoost
UNIQUE (UserId, LeagueId, RoundId, BoostDefinitionId);
```

### Step 2: Update Repository to Handle Constraint Violation

**File:** `PredictionLeague.Infrastructure/Repositories/Boosts/BoostWriteRepository.cs`

```csharp
public async Task<(bool Inserted, string? Error)> InsertUserBoostUsageAsync(
    string boostCode,
    string userId,
    int leagueId,
    int roundId,
    int seasonId,
    CancellationToken cancellationToken)
{
    const string getBoostIdSql = """
        SELECT [Id]
        FROM [BoostDefinitions]
        WHERE [Code] = @BoostCode
        """;

    var boostId = await Connection.QuerySingleOrDefaultAsync<int?>(
        new CommandDefinition(getBoostIdSql, new { BoostCode = boostCode }, cancellationToken: cancellationToken));

    if (boostId is null)
        return (false, "Invalid boost code");

    const string insertSql = """
        INSERT INTO [UserBoostUsages]
        ([UserId], [BoostDefinitionId], [LeagueId], [RoundId], [SeasonId], [AppliedAtUtc])
        VALUES
        (@UserId, @BoostDefinitionId, @LeagueId, @RoundId, @SeasonId, @AppliedAtUtc)
        """;

    var insertCommand = new CommandDefinition(
        insertSql,
        new
        {
            UserId = userId,
            BoostDefinitionId = boostId,
            LeagueId = leagueId,
            RoundId = roundId,
            SeasonId = seasonId,
            AppliedAtUtc = DateTime.UtcNow
        },
        cancellationToken: cancellationToken);

    try
    {
        var rowsAffected = await Connection.ExecuteAsync(insertCommand);
        if (rowsAffected == 0)
            return (false, "Failed to apply boost");

        return (true, null);
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
    {
        // Unique constraint violation - boost already applied
        return (false, "Boost has already been applied to this round");
    }
}
```

### Step 3: Alternative - Use MERGE for Atomic Operation

**File:** `PredictionLeague.Infrastructure/Repositories/Boosts/BoostWriteRepository.cs`

```csharp
public async Task<(bool Inserted, string? Error)> InsertUserBoostUsageAsync(
    string boostCode,
    string userId,
    int leagueId,
    int roundId,
    int seasonId,
    CancellationToken cancellationToken)
{
    const string mergeSql = """
        MERGE INTO [UserBoostUsages] WITH (HOLDLOCK) AS target
        USING (
            SELECT
                @UserId AS UserId,
                bd.[Id] AS BoostDefinitionId,
                @LeagueId AS LeagueId,
                @RoundId AS RoundId,
                @SeasonId AS SeasonId,
                @AppliedAtUtc AS AppliedAtUtc
            FROM [BoostDefinitions] bd
            WHERE bd.[Code] = @BoostCode
        ) AS source
        ON target.[UserId] = source.UserId
           AND target.[LeagueId] = source.LeagueId
           AND target.[RoundId] = source.RoundId
           AND target.[BoostDefinitionId] = source.BoostDefinitionId
        WHEN NOT MATCHED THEN
            INSERT ([UserId], [BoostDefinitionId], [LeagueId], [RoundId], [SeasonId], [AppliedAtUtc])
            VALUES (source.UserId, source.BoostDefinitionId, source.LeagueId, source.RoundId, source.SeasonId, source.AppliedAtUtc)
        OUTPUT $action AS MergeAction;
        """;

    var command = new CommandDefinition(
        mergeSql,
        new
        {
            UserId = userId,
            BoostCode = boostCode,
            LeagueId = leagueId,
            RoundId = roundId,
            SeasonId = seasonId,
            AppliedAtUtc = DateTime.UtcNow
        },
        cancellationToken: cancellationToken);

    var result = await Connection.QuerySingleOrDefaultAsync<string>(command);

    if (result == "INSERT")
        return (true, null);

    return (false, "Boost has already been applied to this round");
}
```

---

## Testing

### Manual Test Steps
1. Create a test league with boost enabled
2. Open two browser tabs logged in as same user
3. Navigate to same round predictions page
4. Attempt to apply boost simultaneously from both tabs
5. Verify only one boost record created in database
6. Verify second request returns appropriate error message

### Verification Query
```sql
-- Check for any duplicate boost applications (should return 0 rows after fix)
SELECT UserId, LeagueId, RoundId, BoostDefinitionId, COUNT(*) AS Count
FROM [UserBoostUsages]
GROUP BY UserId, LeagueId, RoundId, BoostDefinitionId
HAVING COUNT(*) > 1;
```

---

## Rollback Plan

If issues arise:
```sql
-- Remove constraint (will allow race condition again - use only if critical)
ALTER TABLE [UserBoostUsages]
DROP CONSTRAINT UK_UserBoostUsages_UserLeagueRoundBoost;
```

---

## Notes

- The MERGE approach with HOLDLOCK provides the strongest guarantee against race conditions
- The unique constraint approach is simpler but relies on catching the exception
- Both approaches should be considered together - constraint for database-level protection, MERGE/exception handling for clean error responses
