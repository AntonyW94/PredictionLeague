# Architecture: Football API Resilience & Caching

## Status

Not Started | **In Progress** | Complete

> **Scaffolded June 2026.** Transient-fault resilience is already shipped; the
> remaining work is response caching and graceful degradation. See "What exists
> today" before planning - do not rebuild the Polly pipeline.

## Summary

The app depends entirely on api-sports.io for fixtures, scores, teams and
standings, polled as often as once a minute during live windows. Transient-fault
handling (retry, circuit breaker, timeout) is in place, but when the provider is
slow or down for a sustained period, user-facing reads have no last-known data to
fall back on - so features error out rather than degrading gracefully. This plan
adds response caching and a degraded-mode fallback so the site stays useful
during a provider outage, and reduces call volume against the provider's rate
limits.

## User Story

As a player, I want fixtures, scores and standings to keep showing the last known
values when the football data provider is unavailable, so the site stays usable
and I'm told data may be delayed rather than seeing errors.

## What exists today (do NOT rebuild)

- **Retry** with exponential backoff + jitter on 408/429/5xx and transport
  errors - `src/ThePredictions.Infrastructure/Resilience/FootballApiResilienceConfiguration.cs`.
- **Circuit breaker** (50% failure ratio, 30s sampling window, configurable
  minimum throughput and break duration) - same file.
- **Per-request timeout** - same file.
- All tunable via `FootballApiResilienceSettings` (defaults: 3 retries, 30s break,
  30s timeout) - `src/ThePredictions.Application/Configuration/FootballApiResilienceSettings.cs`.
- **Health check** - `src/ThePredictions.Infrastructure/HealthChecks/FootballApiHealthCheck.cs`,
  exposed via `HealthCheckEndpointExtensions`.
- **SQL transient-retry** (separate concern, already done) -
  `src/ThePredictions.Infrastructure/Data/Resilience/SqlRetryPolicy.cs`.

## The gap

- **No caching of API responses.** Every sync and score poll hits the provider
  live. Repeated user reads within a polling window each re-fetch.
- **No degraded-mode fallback.** When the circuit is open or a call fails, the
  dependent read fails rather than serving the last-good value - this is what the
  roadmap means by "site completely fails if the API goes down".
- **No stale-data signal.** Nothing tells users/admin that live data is delayed
  because the provider is unavailable.

## Acceptance Criteria

- [ ] Read-through cache for slow-changing reference data (teams, season/league
      metadata) with a long TTL.
- [ ] Short-TTL cache for live fixtures/scores so repeated reads in a polling
      window don't each hit the provider, retaining the last-good response.
- [ ] When a call fails or the circuit is open, user-facing reads serve the last
      cached value instead of erroring.
- [ ] Cache refresh/invalidation on a successful sync.
- [ ] A stale-data signal (admin/health indicator; optional user-facing banner)
      when live data is unavailable.
- [ ] TTLs configurable via settings, mirroring the `FootballApiResilienceSettings`
      pattern.
- [ ] Tests for cache hit/miss/expiry and degraded-mode fallback.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Cache abstraction & DI | Introduce a caching layer (`IMemoryCache` to start, behind an interface so a distributed cache can swap in later) | Not Started |
| 2 | Cache reference reads | Read-through caching for teams + season/league metadata | Not Started |
| 3 | Live-score cache + last-good retention | Short-TTL cache for fixtures/scores; retain last-good for degraded mode | Not Started |
| 4 | Degraded-mode fallback | On failure/open circuit, serve the last cached value; never hard-fail user reads | Not Started |
| 5 | Stale-data signal | Health/admin indicator + optional user banner when data is stale | Not Started |
| 6 | Tests | Cache hit/miss/expiry + degraded fallback coverage | Not Started |

## Dependencies

- [ ] Aligns with [caching-strategy](../caching-strategy/README.md) - share one
      cache abstraction rather than inventing two.
- [ ] Reuses the existing health-check infrastructure for the stale-data signal.

## Technical Notes

- Transient resilience (retry/circuit-breaker/timeout) is **done** - scope here is
  caching + degradation only. Do not duplicate the Polly pipeline.
- `IMemoryCache` is sufficient for the current single-instance Fasthosts host;
  keep it behind an interface so a distributed cache becomes a drop-in if/when
  scale-out (read-replicas) lands.
- Caching also reduces call volume, helping stay within api-sports.io rate limits.

## Open Questions

- [ ] Acceptable staleness: live scores (~30-60s?) vs reference data (hours/days?).
- [ ] Should a provider outage show a user-facing "scores may be delayed" banner,
      or stay admin-only?
- [ ] Is single-instance `IMemoryCache` enough for launch, deferring a distributed
      cache until scale-out?
