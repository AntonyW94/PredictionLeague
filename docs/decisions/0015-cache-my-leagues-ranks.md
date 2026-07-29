# 0015. Cache the My Leagues ranks on the write path

- **Status:** Accepted
- **Date:** 2026-07-29
- **Deciders:** Antony Willson
- **Tags:** technical

## Context

The **My Leagues** tile shows, per league, a member's overall rank, month (or exact-scores) rank, round
rank and tournament-stage rank, each with the position it moved from. `GetMyLeaguesQueryHandler` computed
all of them live, with one `RANK() OVER (…)` pass per metric over every member of every league the user
was in. That grew to a ~300-line, 8-CTE, ~25-table-reference query.

It was showing up in the slow-query log (`>= 500ms`) at up to 1,217ms in production. Measured on dev - a
tiny database (5 leagues, 82 approved members, 1,616 `LeagueRoundResults` rows) on an idle box:

| | Wall clock | Server execution | Compile |
|---|---|---|---|
| Round-trip floor (`SELECT 1`) | 29ms | - | - |
| The query, cached plan | 93ms | 57ms | ~36ms |
| The query, fresh plan | 400-500ms | 57ms | **~370-440ms** |
| A keyed lookup against the cache | 40ms | - | ~11ms |

So the cost was not the data, and not the ranking work - it was SQL Server **planning** the query, at
roughly seven times what it spent running it. Production repaid that constantly: the score-update job
runs every minute and writes `Matches`, `UserPredictions`, `RoundResults`, `LeagueRoundResults` and
`LeagueMemberStats`; auto-update-statistics then invalidates the cached plan, and the database has
`AUTO_UPDATE_STATISTICS_ASYNC` off, so the next reader also waits for the statistics rebuild before
recompiling. During a live round that is most dashboard loads, which matches the evening clustering of
the warnings. Compilation scales with query size, so collapsing the CTEs does not help: a rewrite that
cut execution from 57ms to 43ms left compile unchanged at ~390ms.

These ranks *used* to be cached in `LeagueMemberStats`. They were moved to live computation to fix four
bugs: blank tiles (no cache row existed), a fabricated "1st" (a missing rank read as 0/1), the Exact
Scores tile ranking by points instead of exact-score count, and a change arrow on round 1. Any decision
to cache again has to not reintroduce those.

The cache was previously maintained as two half-updates - a "snapshot" written when a round went live,
plus a "live" update after each results change - so a cached value was only ever as correct as the
ordering of the calls that produced it. That, not caching itself, is what produced the bugs.

Separately: an earlier investigation found the same query blocking for up to 7.8s behind the write path
because `READ_COMMITTED_SNAPSHOT` is off and cannot be enabled on this Fasthosts-managed instance. That
was mitigated by bracketing the read in `READ UNCOMMITTED`, which remains in place. This record is about
the residual cost once blocking was gone.

## Decision

**We will cache every My Leagues rank in `LeagueMemberStats`, maintained by a single idempotent
recompute, and reduce the query to a keyed lookup.**

1. **One recompute, not incremental updates.** `LeagueStatsRepository` exposes exactly two methods -
   `RefreshLeagueAsync(leagueId)` and `RefreshSeasonAsync(seasonId)` - both running the same batch. It
   derives every cached value from the current results plus the league's active round, so it is
   idempotent and order-independent: running it twice, or late, gives the same answer. The four previous
   round-scoped methods (`SnapshotRanksForRoundStartAsync`, `UpdateLiveStatsAsync`,
   `UpdateStableStatsAsync`, `EnsureMemberStatsRowsExistAsync`) are gone, as is the pass-through
   `ILeagueStatsService`; command handlers depend on the repository directly, per the CQRS rule in
   `CLAUDE.md`.

2. **No snapshots.** A "pre-round" rank is just a rank over the rounds before the active one, which is a
   function of current data - it does not need to have been captured at that moment. Removing the
   point-in-time write removes the whole class of "snapshot taken at the wrong moment" bugs.

3. **Absent means `NULL`, never 0 or 1.** The rank columns are nullable. A league whose season has no
   active round has no rank; a first round of a season, month or stage has no pre-round position; a
   season with no stage mapping has no stage rank. The `DEFAULT ((0))` constraints are dropped, because a
   default is exactly how a missing rank previously surfaced as a fabricated position.

4. **Each column means one metric.** `MonthRank` is always month-points rank and `ExactScoresRank` is
   always exact-score-count rank. The tournament swap happens in one place, the read query, so a
   tournament can never show a points rank in the Exact Scores slot.

5. **Refresh on every event that can move a rank, plus a time-based backstop.** Results changes refresh
   the season; membership changes refresh the league; the per-minute job refreshes every active season
   unconditionally.

6. **The definitions are a contract between two files.** `LeagueStatsRepository.RecomputeAsync` and
   `GetMyLeaguesQueryHandler` must agree on the active-round resolution and on what each rank means.
   Both carry a comment saying so.

## Consequences

**For / positive**
- The read drops from ~400ms of compilation plus 57ms of execution to a keyed lookup measured at ~11ms
  of work, and is small enough that it stops being worth recompiling.
- The ranking work happens once a minute for everybody instead of once per page view per person.
- Correctness is now a property of the recompute rather than of call ordering. Verified against the live
  query on dev: all eight live-computed rank families matched exactly on all 82 league/member rows.
- Two latent bugs fixed on the way: `MonthRank` was ranking by `MONTH(GETUTCDATE())` rather than the
  active round's month, and members with no `LeagueRoundResults` row for a round kept a stale
  `LiveRoundPoints` instead of dropping to zero.
- `StableRoundRank` now refers to the same round as the rest of the tile. It previously kept whichever
  round was last processed, so a finished season could show a round-38 rank under a "Round 1" label.
- Deleting a league no longer trips the non-cascading `LeagueMemberStats` foreign key.

**Against / cost**
- A member's rank can be up to a minute stale for the things only the backstop catches: the active round
  rolling into a new month or stage, and a completed round ageing out of the 48-hour window during which
  the tile keeps showing it. Neither fires a write anywhere, so no explicit trigger is possible.
- Any future way for an approved member to leave a league must refresh that league. The recompute itself
  handles it (it derives the ranked set from who is currently approved), but it has to be called.
- The recompute always does the whole league rather than a delta. That is deliberate - it is what makes
  it idempotent - but it means cost scales with league size, not with what changed.

**Neutral / notes**
- `SnapshotOverallRank` / `SnapshotMonthRank` keep their names so migration `0006` stays purely additive
  and can be applied ahead of the deploy. The names are now misleading; the schema guide says so.
- Backfill is `RecalculateSeasonStatsCommand`, which now ends with a season refresh. Needed for seasons
  the per-minute job never visits, since it only walks seasons still flagged active.
- The `READ UNCOMMITTED` bracket stays. If `READ_COMMITTED_SNAPSHOT` is ever enabled it can be removed.

## Alternatives considered

- **Collapse the rank CTEs into one aggregate pass.** Measured: execution 57ms → 43ms, compile unchanged
  at ~390ms, output byte-identical for all 35 dev users. Does not address the actual cost.
- **`OPTION (KEEPFIXED PLAN)`.** Stops statistics changes invalidating the plan, which is the specific
  trigger here, and is a one-line change. Kept in reserve as a stopgap; rejected as the fix because it
  freezes the plan as data grows and leaves a 300-line query on the read path.
- **`READ_COMMITTED_SNAPSHOT ON` / `AUTO_UPDATE_STATISTICS_ASYNC ON`.** Both would help and neither is
  available: no `ALTER DATABASE` permission on the managed instance. Worth asking Fasthosts for.
- **Cache the endpoint response.** The tile is fetched once per dashboard navigation rather than polled
  (`RefreshLiveDataAsync` only re-fetches active rounds and leaderboards), so this only helps repeat
  navigations and does nothing for the first paint that the user actually waits on.
- **Keep the snapshot/live split and just add the missing columns and triggers.** Rejected: it preserves
  the ordering dependency that caused every previous bug in this table.

## Related

- `docs/todo/architecture/my-leagues-tile-cache/README.md` - the plan this implements.
- Migration `tools/ThePredictions.DatabaseTools/Migrations/0006_LeagueMemberStatsCachedRanks.sql`.
- `docs/guides/database-schema.md#leaguememberstats` - the column contract.
- [ADR-0013](./0013-database-migrations-dbup.md) - why the schema change is a DbUp migration.
