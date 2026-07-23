# Drop The Vestigial RoundResults.TotalPoints Column

## Status

**Not Started** | In Progress | Complete

## Summary

Remove the `TotalPoints` column from the `RoundResults` table. It is a leftover from an older, global scoring model and is no longer maintained or read - it is a trap that has already caused one bug.

## Why it should go

- Points-per-outcome are a **per-league** setting, so points only exist per league and live in `LeagueRoundResults` (`BasePoints` / `BoostedPoints`).
- `RoundResults` is the **global, league-agnostic** per-user-per-round row. It legitimately holds the outcome counts (`ExactScoreCount`, `CorrectResultCount`, `IncorrectCount`) but a single global `TotalPoints` there is meaningless (points in which league?).
- The maintaining MERGE, `RoundRepository.UpdateRoundResultsAsync`, writes only the three counts and never sets `TotalPoints`, so it sits at its default of 0. On prod only a minority of rows are non-zero - stale legacy data.
- **Nothing reads it.** Every leaderboard, record, recap and dashboard query reads points from `LeagueRoundResults`. Verified by searching all queries against `[RoundResults]`.
- It already bit us: the Badges "On the Board" rule originally keyed off `RoundResults.TotalPoints` and missed players who had clearly scored (fixed in commit "Fix On The Board To Use Result Counts Not TotalPoints").

## Acceptance Criteria

- [ ] `RoundResults.TotalPoints` no longer exists in the database.
- [ ] `docs/guides/database-schema.md` updated to drop the column from the RoundResults table.
- [ ] No code references it (confirm with a search; remove the property from the `RoundResult` domain model / any Dapper mapping if present).
- [ ] Build green under `/p:TreatWarningsAsErrors=true`; Domain coverage stays 100%.

## Approach

1. **Confirm zero references** (`grep` for `TotalPoints` scoped to `RoundResults` - the only historical reader, the badge evaluator, no longer uses it). Remove the `TotalPoints` property from the `RoundResult` domain entity and any result record if one exists.
2. **Migration** (new numbered script under [`tools/ThePredictions.DatabaseTools/Migrations/`](../../../../tools/ThePredictions.DatabaseTools/Migrations/)), guarded and idempotent:
   ```sql
   IF COL_LENGTH('dbo.RoundResults', 'TotalPoints') IS NOT NULL
       ALTER TABLE [dbo].[RoundResults] DROP COLUMN [TotalPoints];
   ```
3. **Docs + tools:** update `docs/guides/database-schema.md`; check the `DatabaseTools` refresher copies by explicit column list (if so, drop `TotalPoints` there too so refresh keeps working).

## Migration classification

**Destructive** (drops a column). Per the DbUp rules ([ADR-0013](../../../decisions/)): forward-only, must ship **with or after** the code deploy that no longer references the column (already the case - nothing references it), and take a prod backup (`backup-prod-db.yml`) before applying. Additive/safe it is not, so do not apply it ahead of the deploy.

## Dependencies

- [x] Badges "On the Board" no longer reads the column (already fixed and shipped).
