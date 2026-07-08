# Feature: Live Score Auto-Update (no refresh)

## Status

Not Started | In Progress | **Complete (pragmatic version)**

> **Interval decided:** poll every **10s** (configurable via
> `LivePolling:IntervalSeconds`). Mechanism: client polling now, SignalR
> documented as the later upgrade path (see Open Questions).
>
> **Shipped:** client-side, visibility-aware polling on the league dashboard
> (round results, match scores/statuses and the overall/monthly/exact-scores/
> stage leaderboards) and the user dashboard (Active Rounds and Standings),
> reusing the existing read endpoints. **Intentionally deferred to the
> caching-strategy / football-api-resilience work:** the cache-backed reads
> (Task 4) and the degraded/stale-data banner (Task 5). A failed poll simply
> keeps the last-known values so the page never crashes.

## Summary

Scores are written to the database by the once-a-minute cron
(`UpdateAllLiveScoresCommand`), but the Blazor WebAssembly client does not update
on its own - so a user watching matches has to refresh to see new scores. This
feature adds client-side polling (default every 10s) on live pages so scores,
match statuses and the live leaderboard refresh themselves.

Polling is chosen over SignalR for the current stack (WASM + Fasthosts shared
hosting, ~50-200 users). Given the source data only refreshes ~once a minute, a
10s client poll picks up changes within ~10s of the cron writing them - it
removes the manual refresh without pretending to be real-time-to-the-second.
SignalR (push) is the later upgrade and is captured below rather than built now.

## User Story

As a player following live matches, I want scores and my live league position to
update on their own, so that I don't have to keep refreshing the page.

## Design / Mockup

```
While an in-progress round/match exists AND the tab is visible:
  every 10s  ──▶  GET live snapshot (cache-backed)  ──▶  re-render only if changed
When nothing is live, or the tab is hidden  ──▶  stop polling
```

## Acceptance Criteria

- [x] Live pages auto-refresh every 10s (configurable): the league dashboard
      (round results, match scores/statuses, and the overall, monthly,
      exact-scores and stage leaderboards) and the user dashboard (Active Rounds
      and Standings) update on their own. (The standalone predictions *entry*
      page is pre-deadline only and shows no live scores, so it is not polled.)
- [x] Polling runs only when something is live (an in-progress round/match) and
      pauses when the browser tab is hidden (Page Visibility API).
- [ ] Polled reads are served from the cache layer (see
      [caching-strategy](../../../architecture/caching-strategy/README.md) and
      [football-api-resilience](../../../architecture/football-api-resilience/README.md))
      so each poll is cheap. **Deferred** - the polls reuse the existing read
      endpoints directly; a cache layer is a later, separate piece of work.
- [x] UI re-renders only when data actually changed (no flicker).
- [x] Poll interval is configurable; default 10s.
- [~] Degrades gracefully when the provider/circuit is down: a failed poll keeps
      the last-known values (no crash). The "scores may be delayed" banner
      **depends on the resilience work and is deferred**.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Live snapshot endpoint(s) | Reused the existing reads: `GET /api/rounds/{roundId}/matches-data`, `GET /api/leagues/{leagueId}/rounds/{roundId}/results`, `GET /api/leagues/{leagueId}/leaderboard/overall`. No new endpoint needed | Done (reused) |
| 2 | Client polling service | `LiveScorePollingService` - reusable, visibility-aware `PeriodicTimer` helper with start/stop and configurable interval (default 10s) | Done |
| 3 | Wire live pages | League dashboard (round results + overall/monthly/exact-scores/stage leaderboards) and user dashboard (Active Rounds + Standings), via the state services + `OnStateChange` / `OnLiveDataChanged` | Done |
| 4 | Cache-backed reads | Ensure polled endpoints hit the cache layer | Deferred (caching-strategy work) |
| 5 | Degraded/stale handling | Reflect the resilience plan's stale-data signal | Deferred (football-api-resilience work); failed polls keep last-known values |
| 6 | Tests | Start/stop, visibility pause, interval config, no-live = no poll, change-detection | Done |

## Dependencies

- [ ] [caching-strategy](../../../architecture/caching-strategy/README.md) and
      [football-api-resilience](../../../architecture/football-api-resilience/README.md)
      - so polls are cheap and degrade gracefully.
- [x] Server-side live scores pipeline (cron + `UpdateAllLiveScoresCommand`) -
      already shipped.

## Technical Notes

- Stack is Blazor WebAssembly + a separate API, so this is plain authenticated
  HTTP polling reusing the existing `HttpClient` - **no new infrastructure**.
- **Cron ceiling:** scores are only as fresh as the 1-minute provider poll. The
  10s client poll removes the manual refresh and picks changes up quickly; it does
  not make scores more real-time than the cron.
- Pause polling on a hidden tab (Page Visibility API) and when there is no
  in-progress round, to limit request volume and battery use.
- **"Live" means a match is actually in progress**, keyed on `MatchStatus.InProgress`,
  not `RoundStatus.InProgress`. A round stays in progress for the whole gameweek
  (often days) while matches only play in short windows, so keying on round status
  would poll continuously through the gaps. Trade-off: polling starts when a match
  is in progress at page load (or once one kicks off while already polling); it does
  not auto-start at the very first kick-off if the page was opened beforehand, so a
  user sitting on the page pre-kick-off would need to reload. That is an acceptable
  cost for not hammering the API for days per round.

## Open Questions

- [x] Which existing endpoints can be reused as the "live snapshot" vs. needing a
      slimmer dedicated one? **Resolved:** the three existing reads
      (`matches-data`, per-round `results`, `leaderboard/overall`) cover it - no
      new endpoint was added. A slim combined snapshot endpoint could be added
      later alongside the cache layer if the three-call fan-out proves too chatty.
- [x] Should the live leaderboard re-rank live, or only the scores? **Resolved:**
      the leaderboards already re-rank live server-side (`RANK()` over
      `SUM(BoostedPoints)` with `IsRoundInProgress` / `SnapshotRank`), so polling
      them refreshes live ranks and the up/down arrows. The league dashboard's
      overall, monthly, exact-scores and stage leaderboards and the user
      dashboard's Standings are all live-refreshed.
- [ ] **SignalR upgrade trigger:** define the threshold at which push becomes
      worth it - e.g. score cadence drops below ~30s, provider webhooks/streaming
      become available, or the app moves to a host with confirmed WebSocket
      support (or Azure SignalR Service). Do **not** build SignalR while on
      Fasthosts shared hosting without first confirming WebSocket support; its
      fallback transports (SSE/long-poll) hold connections open and stress
      shared-host connection limits.
