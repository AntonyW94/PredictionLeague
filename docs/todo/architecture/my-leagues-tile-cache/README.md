# My Leagues Tile Cache

## Status

**Not Started** | In Progress | Complete

## Goal

Make the **Leagues tab** (the `MY LEAGUES` card) load fast again by reading the four
tile ranks from the `LeagueMemberStats` cache instead of computing them live, **without
re-introducing the bugs we just fixed** (blank tiles, fabricated 1st, points-vs-exact-score
confusion, spurious round-1 arrows).

## Background — why this plan exists

`GetMyLeaguesQueryHandler` originally read most ranks from the `LeagueMemberStats` cache.
A series of bugs (missing rows, stale values, the "Exact Scores" tile ranking by points
instead of exact-score count, and a round-1 change arrow) were fixed by moving each tile to
**live computation** inside the query. That made the tiles always correct but added several
`RANK() OVER (…)` CTEs (`ActiveOverallRanks`, `ActiveRoundRanks`, `ActiveStageRanks`,
`ActiveRoundMonthlyRanks`, `ActiveExactScoresRanks`), each grouping over
`LeagueRoundResults` / `RoundResults` for every member of every league the user is in.
That is the load that made the tab slow.

This plan moves the heavy per-member ranking **back to the write-path** (where it runs once
per match-results update, not once per page view), stores the **correct** values in
`LeagueMemberStats`, and reverts the query to simple reads.

**The golden rule that prevents the old bugs:** the write-path must store *exactly the metric
each tile displays*, and must run on *every event that can change a member's rank*. The two
mistakes before were (a) storing the wrong metric (points where exact-score count was needed)
and (b) not refreshing the cache (so rows were missing or stale). Both are addressed below.

## What each tile shows (the contract the cache must honour)

| Tile | Metric (per league) | Live CTE today | Cache column(s) |
|------|---------------------|----------------|-----------------|
| Overall Rank | RANK by SUM of **all** rounds' `BoostedPoints` | `ActiveOverallRanks` | `OverallRank` (exists), `PreRoundOverallRank` |
| Round N | RANK by the **active round's** `BoostedPoints` | `ActiveRoundRanks` | `LiveRoundRank` (exists), `StableRoundRank` (exists) |
| Group Stage | RANK by SUM of the **stage's** rounds' `BoostedPoints` | `ActiveStageRanks` | `StageRank` (NEW), `PreRoundStageRank` (NEW) |
| Exact Scores *(tournament)* | RANK by SUM of season `RoundResults.ExactScoreCount` | `ActiveExactScoresRanks` | `ExactScoresRank` (NEW), `PreRoundExactScoresRank` (NEW) |
| Month *(league competition)* | RANK by SUM of the **month's** rounds' `BoostedPoints` | `ActiveRoundMonthlyRanks` | `MonthRank` (exists), `PreRoundMonthRank` |

> The "Exact Scores" and "Month" tiles share the `MonthRank` DTO field — the tile is relabelled
> by `CompetitionType`. The cache must therefore populate `MonthRank` with **exact-score count
> rank** for tournaments (`CompetitionType = 1`) and **month points rank** for league
> competitions (`CompetitionType = 0`). This is the exact mistake that showed 9th instead of 3rd —
> do not store points rank for a tournament's `MonthRank`.

The authoritative "correct" SQL for every metric already exists in the live query
(`GetMyLeaguesQueryHandler`) and, for cross-checking, in the leaderboard handlers
(`GetOverallLeaderboardQueryHandler`, `GetExactScoresLeaderboardQueryHandler`). The write-path
SQL below is those same expressions, partitioned by league.

## 1. Schema changes (additive migration)

Run against each environment **before** deploying the code that reads the new columns.
All additive (new nullable columns + widening two existing columns to nullable), so it is safe
to apply ahead of the deploy.

```sql
-- New cached ranks for the Group Stage and Exact Scores tiles
ALTER TABLE [LeagueMemberStats] ADD [StageRank]                INT NULL;
ALTER TABLE [LeagueMemberStats] ADD [PreRoundStageRank]        INT NULL;
ALTER TABLE [LeagueMemberStats] ADD [ExactScoresRank]          INT NULL;
ALTER TABLE [LeagueMemberStats] ADD [PreRoundExactScoresRank]  INT NULL;

-- Optional but recommended: store the underlying measures for tie-break visibility,
-- digests and debugging (mirrors the existing LiveRoundPoints/StableRoundPoints columns)
ALTER TABLE [LeagueMemberStats] ADD [StagePoints]       INT NULL;
ALTER TABLE [LeagueMemberStats] ADD [ExactScoresCount]  INT NULL;

-- Allow the pre-round snapshots to be NULL so "no prior round" => no arrow, matching the
-- live behaviour. They are currently NOT NULL DEFAULT 0.
ALTER TABLE [LeagueMemberStats] ALTER COLUMN [SnapshotOverallRank] INT NULL;
ALTER TABLE [LeagueMemberStats] ALTER COLUMN [SnapshotMonthRank]   INT NULL;
```

Per `CLAUDE.md`, do **not** commit a `.sql` file — apply the above by hand. The block here is
documentation, not a migration script.

`PreRoundOverallRank` and `PreRoundMonthRank` map onto the existing `SnapshotOverallRank` /
`SnapshotMonthRank` columns (now nullable). `PreRoundStageRank` / `PreRoundExactScoresRank` are
new because there were never stage/exact-score snapshots.

### Also update (non-code)

- `docs/guides/database-schema.md` — add the new columns to the `LeagueMemberStats` table and
  mark `SnapshotOverallRank` / `SnapshotMonthRank` nullable.
- `tools/ThePredictions.DatabaseTools` — `LeagueMemberStats` is already copied; new columns flow
  automatically via `SELECT *`. No anonymisation needed (no personal data). No change required
  beyond confirming the column list isn't hard-coded anywhere.

## 2. Write-path changes (where the ranks are computed)

All ranking moves into `LeagueStatsRepository`, invoked from the existing
`UpdateMatchResultsCommandHandler` flow (and the per-minute self-heal in
`UpdateScoresForNextRoundCommandHandler`). The methods already exist; extend them.

### `UpdateLiveStatsAsync(roundId)` — the "current standings" recompute

Already recomputes `LiveRoundRank` / `OverallRank` / `MonthRank` for every member of every
league in the round's season. Extend the same `UPDATE … FROM (RANK() OVER …)` to also set:

- `StageRank` — RANK by SUM of `BoostedPoints` for rounds in the **same stage** as the active
  round (join `TournamentRoundMappings` / `RoundStages` as the live `ActiveStageRanks` does).
- `ExactScoresRank` — RANK by SUM of `RoundResults.ExactScoreCount` for the season (as
  `ActiveExactScoresRanks` / `GetExactScoresLeaderboardQueryHandler`).
- For tournaments, `MonthRank` should be **set equal to `ExactScoresRank`** *or* the tile-read
  CASE (see §4) should choose `ExactScoresRank`. Pick one place to apply the tournament/league
  switch and document it — recommendation: keep `MonthRank` = month-points rank in the cache and
  apply the `CompetitionType` switch at read time, so the cache column names mean one thing each.
- Optionally persist `StagePoints` and `ExactScoresCount` alongside.

Copy the exact `ORDER BY` expressions from the live CTEs so the cached numbers are identical to
what the query produced live. Cross-check against the leaderboard handlers.

### `UpdateStableStatsAsync(roundId)`

Unchanged in scope (already maintains `StableRoundRank` / `StableRoundPoints` from completed
matches). Confirm it still runs before `UpdateLiveStatsAsync` in the handler.

### `SnapshotRanksForRoundStartAsync(roundId)` — the "pre-round" baseline

Currently sets `SnapshotOverallRank = OverallRank`, `SnapshotMonthRank = MonthRank` and resets
the live round fields. Extend to snapshot the new pre-round values **as they stand before the
round starts**:

- `SnapshotOverallRank`, `SnapshotMonthRank`, `PreRoundStageRank`, `PreRoundExactScoresRank`.
- **First-round rule:** if the round starting is the first round of its season / stage / month
  (i.e. no earlier round is `InProgress`/`Completed`), set the corresponding pre-round column to
  `NULL` so the tile shows **no arrow**. This is the cached equivalent of the live
  `WHEN NOT EXISTS (… earlier round …) THEN NULL` guard, and is what stops the round-1 arrow.

### `EnsureMemberStatsRowsExistAsync(roundId)`

Already creates missing rows. Keep it — it is the safety net that stopped blank tiles. New
columns default to `NULL`, which is fine until the first recompute populates them.

## 3. Refresh triggers (the correctness-critical checklist)

A cache is only as correct as its refresh coverage. Every event below changes at least one
member's rank and **must** trigger a recompute for the affected league(s). Verify each:

- [x] **Match result entered / changed** → `UpdateMatchResultsCommandHandler` already calls
      `UpdateStableStatsAsync` + `UpdateLiveStatsAsync`. (Covers the common case.)
- [x] **Round starts** (Published → InProgress) → `TakeRoundStartSnapshotsAsync`. (Sets pre-round
      baselines.)
- [x] **Per-minute self-heal** → `UpdateScoresForNextRoundCommandHandler` ensures rows and (after
      this change) should recompute while a round is in progress. Broaden it to always recompute
      for the active in-progress round, not only when it just created rows.
- [ ] **A member joins / is approved mid-season** → ranks shift for *everyone* in that league
      (denominator changes; their 0 points reorders ties). Add a recompute of that league's stats
      on approval (`UpdateLeagueMemberStatusCommandHandler` / `JoinLeagueCommandHandler`). This is
      the one trigger not currently wired — without it a new joiner sees stale ranks until the next
      results update.
- [ ] **A member is removed / leaves** → same reasoning; recompute the league.
- [ ] **Boost applied/auto-applied** → already inside the match-results flow
      (`ApplyRoundBoostsAsync` runs before `UpdateLiveStatsAsync`). Confirm ordering.

If a trigger is genuinely impractical to wire, the per-minute self-heal is the backstop —
but prefer explicit triggers so the tile is correct immediately, not up to a minute later.

## 4. Query revert (`GetMyLeaguesQueryHandler`)

Delete the `ActiveOverallRanks`, `ActiveRoundRanks`, `ActiveStageRanks`,
`ActiveRoundMonthlyRanks` and `ActiveExactScoresRanks` CTEs and their joins. Read from
`LeagueMemberStats stats` instead:

```text
Rank                  <- stats.[OverallRank]
RoundRank             <- CASE WHEN ar.Status = Published THEN 1 ELSE stats.[LiveRoundRank] END
StableRoundRank       <- CASE WHEN ar.Status = Published THEN 1 ELSE stats.[StableRoundRank] END
StageRank             <- stats.[StageRank]
PreRoundStageRank     <- stats.[PreRoundStageRank]
MonthRank             <- CASE WHEN CompetitionType = 1 THEN stats.[ExactScoresRank] ELSE stats.[MonthRank] END
PreRoundMonthRank     <- CASE WHEN CompetitionType = 1 THEN stats.[PreRoundExactScoresRank] ELSE stats.[SnapshotMonthRank] END
PreRoundOverallRank   <- stats.[SnapshotOverallRank]
```

Keep `ActiveRounds`, `RoundStages` and `LeagueContext` (they provide round metadata, the
stage name and prize/member counts — not per-member ranks). The `SELECT` column **order must
stay identical** to the `MyLeagueDto` constructor (Dapper maps positionally — see `CLAUDE.md`).

The only remaining live work is the cheap, league-level round metadata — no per-member
`RANK()` passes. That removes the slow part.

## 5. Backfill for existing data

After the migration + deploy, the new columns are `NULL` for current rows. Populate them once:

- Preferred: trigger the existing recompute for every active in-progress round (e.g. call the
  score-update task, or a one-off admin endpoint that calls `UpdateLiveStatsAsync` for each
  in-progress round). No SQL needed.
- The per-minute self-heal will also populate them on its next tick once §3 is wired.

Pre-round columns for the *current* in-progress round can't be reconstructed perfectly after the
fact (the snapshot moment has passed); accept that the arrow may be absent/approximate for the
in-flight round and correct from the next round start onwards.

## 6. Tests

- Domain: any new `RefreshToken`-style methods are N/A here; ranking is SQL. Keep Domain at 100%.
- Repository/integration: extend the stats-update coverage — assert that after a results update
  the new columns hold the same values the live query produced (golden-master against a small
  fixture). Cover: tournament `ExactScoresRank` = exact-score-count rank (not points);
  `StageRank`; first-round pre-round columns are `NULL`.
- `GetMyLeaguesQueryHandler`: it becomes read-only; if there are handler tests, update expected
  SQL/shape. No behavioural change to the DTO.

## 7. Rollout order

1. Apply the additive migration (§1) to the target DB.
2. Update `database-schema.md`.
3. Deploy the write-path + query-revert code.
4. Backfill (§5).
5. Verify the Leagues tab: numbers match the leaderboards, arrows only from round 2+, and the
   tab loads quickly.

Because the migration is additive and the code reads the new columns with `ISNULL`-tolerant
logic (NULL → no arrow / row self-heals), step 1 can run a little ahead of step 3 without
breaking the currently-deployed (live-computing) code.

## 8. How this avoids the previous mistakes

- **Wrong metric (points vs exact scores):** §What-each-tile-shows pins every cache column to the
  exact metric and source the leaderboard uses; the tournament `MonthRank` switch is called out
  explicitly.
- **Missing rows / blank tiles:** `EnsureMemberStatsRowsExistAsync` stays as the safety net.
- **Stale values:** §3 enumerates every refresh trigger and flags the one currently missing
  (member join/leave).
- **Spurious round-1 arrow:** the first-round `NULL` rule moves into the round-start snapshot.

The decision to cache (and the metrics chosen) should be recorded in `docs/decisions/` per
`CLAUDE.md` when this is implemented.
