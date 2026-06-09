# Feature: Live Score Auto-Update (no refresh)

## Status

**Not Started** | In Progress | Complete

> **Interval decided:** poll every **10s** (configurable). Mechanism: client
> polling now, SignalR documented as the later upgrade path (see Open Questions).

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

- [ ] Live pages auto-refresh every 10s (configurable): the predictions view
      during an in-progress round, and the league live round / leaderboard.
- [ ] Polling runs only when something is live (an in-progress round/match) and
      pauses when the browser tab is hidden (Page Visibility API).
- [ ] Polled reads are served from the cache layer (see
      [caching-strategy](../../../architecture/caching-strategy/README.md) and
      [football-api-resilience](../../../architecture/football-api-resilience/README.md))
      so each poll is cheap.
- [ ] UI re-renders only when data actually changed (no flicker).
- [ ] Poll interval is configurable; default 10s.
- [ ] Degrades gracefully when the provider/circuit is down: shows last-known
      values plus an optional "scores may be delayed" hint (per the resilience plan).

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Live snapshot endpoint(s) | Reuse/trim a lightweight GET for current scores + statuses for a round (and live leaderboard); `GetMatchesForRoundQuery` / `RoundsController` matches-data may already cover most of it | Not Started |
| 2 | Client polling service | Reusable, visibility-aware polling helper (`PeriodicTimer`) with start/stop and a configurable interval (default 10s) | Not Started |
| 3 | Wire live pages | Predictions in-progress view + league live round/leaderboard | Not Started |
| 4 | Cache-backed reads | Ensure polled endpoints hit the cache layer | Not Started |
| 5 | Degraded/stale handling | Reflect the resilience plan's stale-data signal | Not Started |
| 6 | Tests | Start/stop, visibility pause, interval config, no-live = no poll | Not Started |

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

## Open Questions

- [ ] Which existing endpoints can be reused as the "live snapshot" vs. needing a
      slimmer dedicated one?
- [ ] Should the live leaderboard re-rank live, or only the scores? (live ranks
      already exist via `LeagueMemberStats` live fields.)
- [ ] **SignalR upgrade trigger:** define the threshold at which push becomes
      worth it - e.g. score cadence drops below ~30s, provider webhooks/streaming
      become available, or the app moves to a host with confirmed WebSocket
      support (or Azure SignalR Service). Do **not** build SignalR while on
      Fasthosts shared hosting without first confirming WebSocket support; its
      fallback transports (SSE/long-poll) hold connections open and stress
      shared-host connection limits.
