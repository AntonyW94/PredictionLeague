# Query Performance Monitoring

## Status

Not Started | **In Progress (slow-query logging shipped)** | Complete

> **Shipped (2026-07-25):** read queries are now timed and any that meet or exceed a configurable
> threshold are logged at Warning level. `QueryMonitoringSettings.SlowQueryThresholdMilliseconds`
> (default **500ms**, bound from the `QueryMonitoring` config section) drives it; the timing wraps
> every read that goes through `DapperReadDbConnection` (`QueryAsync` / `QuerySingleOrDefaultAsync`).
> Parameters are never logged - the SQL is parameterised, so the text carries no user data.

## Summary

Track slow queries and identify missing indexes for database performance optimisation.

## Priority

**Medium**

## Requirements

- [x] Log slow queries (configurable threshold, default 500ms) - `DapperReadDbConnection` + `QueryMonitoringSettings`.
- [x] Track query execution times (per read, via the same timing).
- [ ] **Identify missing indexes** - review the slow queries that surface in the logs and add indexes where warranted. This is the remaining, non-hands-off work: it needs judgement and prod query analysis.
- [ ] Set up alerts for slow queries - belongs to [`../alerting-config/`](../alerting-config/) (Datadog monitors over the Warning logs), not here.
- [ ] Create performance dashboard - belongs to [`../apm-integration/`](../apm-integration/).

## Technical Notes

The write side (repositories) is **not** timed by this change - only the read path
(`IApplicationReadDbConnection`), which is where the leaderboard/dashboard/record queries run and
where slow reads matter most. Extending timing to the command/repository side can be added later if a
slow write path is suspected.

Remaining options for the index work:
- Read the Warning logs for recurring slow queries, then add covering indexes.
- SQL Server Query Store (if available on Fasthosts) for server-side capture.
- Datadog APM query tracing (if the APM plan lands).

## What the July 2026 investigation established

The slow-query warnings were dominated by the My Leagues dashboard read. Two findings from chasing
it are worth keeping, because both constrain how any future slow query can be diagnosed or fixed
here.

**Read-committed snapshot isolation is off, and cannot be turned on.** The production database has
`is_read_committed_snapshot_on = False`, so reads take shared locks and queue behind the write
transaction that the score-update cron runs every minute during live rounds. This was proved by
reproduction rather than inferred: holding `SELECT ... WITH (TABLOCKX)` on `[LeagueRoundResults]` in
one connection took a normal read from 797ms to 7,128ms, while a `READ UNCOMMITTED` read stayed at
717ms under the same lock. `ALTER DATABASE ... SET READ_COMMITTED_SNAPSHOT ON` is the right fix and
is **blocked**: neither the user's login nor the read logins have `ALTER DATABASE` on the
Fasthosts-managed instance (Msg 5011), and there is no admin login. It would need Fasthosts to run
it on request. Meanwhile the two worst offenders (`GetMyLeaguesQueryHandler`,
`GetLeagueRecordsQueryHandler`) bracket their SQL in `READ UNCOMMITTED`, accepting transient dirty
reads on an auto-refreshing tile. Apply the same treatment if another dashboard read starts
recurring in the logs.

**Plan compilation, not execution, was the residual cost.** After the isolation bracket shipped, the
warnings continued at around 1,217ms. Measured on an idle dev database, the query executed in 57ms
server-side but took 370-440ms to *compile*; roughly 90% of a slow run was the optimiser planning a
300-line, 8-CTE, ~25-table-reference statement. Production repaid that constantly because the
per-minute score-update writes trigger auto-update-stats, which invalidates the cached plan - and
`is_auto_update_stats_async_on = 0` means the next reader waits synchronously for the stats rebuild
before recompiling. That is what made materialising the ranks
([ADR-0015](../../../decisions/0015-cache-my-leagues-ranks.md)) the correct fix: the recompute is one
~80-row `MERGE` a minute instead of a fresh plan per page view. If the same shape appears again,
`OPTION (KEEPFIXED PLAN)` is a verified tactical stopgap that stops the stats-driven recompiles.

Note that no execution plans are available: the read logins have neither Query Store, DMV access
(`VIEW DATABASE PERFORMANCE STATE` is denied) nor `SHOWPLAN`. Compile cost has to be measured by
timing a cache-busted run (prepend a unique comment) against `SET STATISTICS TIME` server elapsed.

## Index gaps found, none yet addressed

All additive, and none was the bottleneck at the time - which is why they are still here rather than
in a migration. Re-measure before adding any of them:

| Table | Gap |
|-------|-----|
| `LeagueMembers` | Clustered PK is `(LeagueId, UserId)`, so the "which leagues am I in" filter on `UserId` scans |
| `Matches` | Only `PK(Id)`, so the per-round match-count subqueries scan |
| `RoundResults` | Unique key is `(RoundId, UserId)`, so lookups by `UserId` scan |
| `Winnings` | Only `PK(Id)`, though it is joined on `LeaguePrizeSettingId` |
| `LeaguePrizeSettings` | Only `PK(Id)`, though it is filtered on `LeagueId` |
