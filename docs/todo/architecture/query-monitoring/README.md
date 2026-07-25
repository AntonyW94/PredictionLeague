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
