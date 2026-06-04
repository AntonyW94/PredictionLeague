# Task 5: Read Queries

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Carry the four ET/penalty columns from the database into `MatchInRoundDto` via
the two query handlers that build it — without tripping Dapper's positional
mapping in `GetRoundByIdQueryHandler`.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Application/Features/Dashboard/Queries/GetMatchesForRoundQueryHandler.cs` | Modify | Add columns to `SELECT` (Dapper maps by name) |
| `src/ThePredictions.Application/Features/Admin/Rounds/Queries/GetRoundByIdQueryHandler.cs` | Modify | `SELECT` + result record + constructor call (positional — lockstep) |

## Implementation Steps

### Step 1: `GetMatchesForRoundQueryHandler` (maps **by name**)

`QueryAsync<MatchInRoundDto>` matches constructor params to columns by name, so
just add the four columns to the `SELECT` with exactly matching names:

```sql
m.[ActualHomeTeamScore],
m.[ActualAwayTeamScore],
m.[Status],
m.[PlaceholderHomeName],
m.[PlaceholderAwayName],
m.[AfterExtraTimeHomeScore],
m.[AfterExtraTimeAwayScore],
m.[PenaltyHomeScore],
m.[PenaltyAwayScore]
```

### Step 2: `GetRoundByIdQueryHandler` (maps **positionally** — careful)

This handler reads into a private `RoundQueryResult` record and then calls
`new MatchInRoundDto(...)` positionally. Per the project's Dapper rule, do all
three in lockstep:

1. **`SELECT`** — add the four columns after `m.[PlaceholderAwayName]`:

   ```sql
   m.[PlaceholderHomeName],
   m.[PlaceholderAwayName],
   m.[AfterExtraTimeHomeScore],
   m.[AfterExtraTimeAwayScore],
   m.[PenaltyHomeScore],
   m.[PenaltyAwayScore]
   ```

2. **`RoundQueryResult` record** — append the four fields in the **same order**:

   ```csharp
   string? PlaceholderHomeName,
   string? PlaceholderAwayName,
   int? AfterExtraTimeHomeScore,
   int? AfterExtraTimeAwayScore,
   int? PenaltyHomeScore,
   int? PenaltyAwayScore
   ```

3. **`new MatchInRoundDto(...)`** — append the four args after
   `r.PlaceholderHomeName, r.PlaceholderAwayName`:

   ```csharp
   r.PlaceholderHomeName,
   r.PlaceholderAwayName,
   r.AfterExtraTimeHomeScore,
   r.AfterExtraTimeAwayScore,
   r.PenaltyHomeScore,
   r.PenaltyAwayScore
   ```

## Verification

- [ ] `SELECT` column order ↔ `RoundQueryResult` constructor order match by name + type, top to bottom.
- [ ] Round details endpoint returns the four values for a knockout match that went to ET/pens, and `null` for group/league matches.
- [ ] No `InvalidOperationException` (materialisation) at runtime — exercise the admin round details + league dashboard round results paths.

## Notes

- A positional mismatch here **compiles and passes unit tests** but throws at runtime — verify against a real round, not just the build.
</content>
