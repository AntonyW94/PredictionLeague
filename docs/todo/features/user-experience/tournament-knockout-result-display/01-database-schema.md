# Task 1: Database Schema & Migration

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add four nullable score columns to the `Matches` table to hold the
after‑extra‑time aggregate and the penalty‑shootout tally, and document them.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| *(database)* | Migration SQL | Add the four columns — **present in chat, do NOT commit a `.sql` file** |
| `docs/guides/database-schema.md` | Modify | Document the four new `Matches` columns |

## New Columns on `Matches` (all nullable, additive)

| Column | Type | Null | Meaning |
|--------|------|------|---------|
| `AfterExtraTimeHomeScore` | `int` | YES | Home score after extra time (API `goals.home`). Set for knockout matches past 90′. |
| `AfterExtraTimeAwayScore` | `int` | YES | Away score after extra time (API `goals.away`). |
| `PenaltyHomeScore` | `int` | YES | Home penalty‑shootout score (API `score.penalty.home`). Set only when there is a shootout. |
| `PenaltyAwayScore` | `int` | YES | Away penalty‑shootout score (API `score.penalty.away`). |

`ActualHomeTeamScore` / `ActualAwayTeamScore` are unchanged (still the 90‑minute
score for knockouts). Group‑stage and league matches leave all four `NULL`.

## Implementation Steps

### Step 1: Migration SQL (additive — safe to apply ahead of the code deploy)

Present this in chat for the user to run manually (per the project rule on not
committing `.sql` files):

```sql
ALTER TABLE [Matches] ADD
    [AfterExtraTimeHomeScore] INT NULL,
    [AfterExtraTimeAwayScore] INT NULL,
    [PenaltyHomeScore] INT NULL,
    [PenaltyAwayScore] INT NULL;
```

### Step 2: Update `docs/guides/database-schema.md`

Add the four columns to the `Matches` table section (around the current
lines 271–297), matching the existing table's column/format style.

## DatabaseTools — no code change needed (but state why)

`tools/ThePredictions.DatabaseTools/DatabaseRefresher.cs` copies each table with
`SELECT *` and dynamically intersects source/target columns
(`DatabaseRefresher.cs:122,286`), so the new columns are copied automatically.
`Matches` is already in `TableCopyOrder`. The columns are **not** personal data,
so `DataAnonymiser` and `PersonalDataVerifier` need no changes. Note this
explicitly in the PR so a reviewer knows it was considered.

## Verification

- [ ] Migration applies cleanly to a database with existing `Matches` rows (all four columns `NULL`).
- [ ] `docs/guides/database-schema.md` lists the four columns with correct types/nullability.
- [ ] No `.sql` file committed anywhere in the repo.

## Notes

- This is the only schema change in the feature; everything else builds on it.
</content>
