# Feature: Displaying Knockout Results (Extra Time & Penalties)

## Status

Not Started | **In Progress (plan only)** | Complete

## Summary

Tournament knockout matches can finish after extra time (AET) or a penalty
shootout (PEN). The **prediction is already scored on the 90‑minute result** and
that must not change. What is missing is the **display**: today we discard the
AET/penalty data, so a knockout decided on penalties renders as e.g. `1 - 1`
with a green tick — indistinguishable from a draw, which is impossible in a
knockout and looks wrong.

This feature:

1. **Captures** the after‑extra‑time aggregate and penalty‑shootout score
   (running, on every poll — not just at the final whistle).
2. **Keeps the 90‑minute score as the primary, scored result** everywhere.
3. **Adds a secondary, less‑prominent caption** under the result — a **live
   ticker** (`Extra time 2-1` / `Penalties 3-2`) that settles to `2-1 (a.e.t.)`
   / `4-2 pens` once decided.
4. **Explains the rule once per round** with a small legend.

> Read [`tournament-knockout-scoring`](../tournament-knockout-scoring/README.md)
> first — this is the **display** follow‑on to that **scoring** feature.

## User Story

As a league member watching a knockout match that goes beyond 90 minutes, I want
to see that the game is scored on the 90‑minute result while still seeing how
extra time and penalties are unfolding, so the result is never confusing (a
penalty win must not look like a draw).

## Design / Decisions

### Caption wording (decided)

| Situation | Caption | Notes |
|-----------|---------|-------|
| Extra time **in progress** | `Extra time 2-1` | live; 90′ score is the pulsing headline above |
| Penalties **in progress** | `Penalties 3-2` | live |
| Decided in extra time | `2-1 (a.e.t.)` | after‑ET score = API `goals` |
| Decided on penalties | `4-2 pens` | shootout score only; after‑ET kept in tooltip |
| Decided in 90 minutes | *(no caption)* | unchanged |

* **No `Final:` prefix** — the primary badge already says "this is the result";
  `(a.e.t.)` / `pens` carry the meaning, and it keeps the caption short for the
  narrow desktop column.
* **Penalties show only the shootout score.** The decisive info is the shootout;
  the level score is already in the 90′ badge. If goals are scored *in* extra
  time before pens, the after‑ET aggregate is retained in the **tooltip** so
  nothing is lost.
* **Primary is always the 90‑minute score** and never changes — no jarring
  "jump back" at the final whistle.

### Match status during extra time / penalties (decided: presentation only)

The match **stays `InProgress`** until the tie resolves, then flips to
`Completed`. We do **not** mark it `Completed` at 90′ — that would stop the score
poller fetching it and trip round‑completion/prize processing mid‑tie, for no
real gain (predictions are already graded at 90′ in the live view). The caption
wording is chosen from the `InProgress` → `Completed` transition, so **no
separate "phase" signal is plumbed to the client**.

A supporting fix is required: `GetMatchStatus` must map the break before extra
time (`BT`), the shootout (`P`) and generic `LIVE` to `InProgress`, otherwise
they fall to `Scheduled` and the score is cleared mid‑match (Task 4).

### Display surface audit (what changes, what doesn't)

Only surfaces that render the **actual match result** are in scope. Surfaces
rendering a **user prediction** never show AET.

| Surface | File | Action |
|---------|------|--------|
| Desktop results grid — actual result | `PredictionGrid.razor` → `MatchStatusBadge` | caption (Task 7) |
| Mobile match card — actual result | `MobileMatchResultCard.razor` → `MatchStatusBadge` | caption (Task 7) |
| Per‑round legend | `RoundResultsTile.razor` | new (Task 8) |
| Admin "Enter Results" | `EnterResults.razor` / view‑models | preserve ET/pens on round‑trip (Task 6) |
| Player prediction badges | `PredictionStatusBadge.razor`, grids | none (predictions are 90′ only) |
| Dashboard active‑round preview | `RoundCard.razor` | none (shows *predicted* score, no actual) |
| Prediction entry steppers | `Predictions.razor` | none |
| Round points leaderboard | `RoundResultsTile.razor` table | none |

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Database Schema & Migration](./01-database-schema.md) | Add four nullable score columns to `Matches`; update schema doc | Not Started |
| 2 | [Domain Model (`Match`)](./02-domain-model.md) | Properties, constructor, `UpdateScore`, `Postpone` + 100% tests | Not Started |
| 3 | [Contracts & Validation](./03-contracts-and-validation.md) | Extend `MatchResultDto` / `MatchInRoundDto`, validator, test builder | Not Started |
| 4 | [Score Capture & Status Mapping](./04-score-capture.md) | `GetScoreForMatch` live+final capture, `GetMatchStatus`, command wiring | Not Started |
| 5 | [Read Queries](./05-read-queries.md) | `GetMatchesForRoundQueryHandler` + `GetRoundByIdQueryHandler` (Dapper positional!) | Not Started |
| 6 | [Admin Enter Results Round‑Trip](./06-admin-round-trip.md) | Preserve ET/pens through the admin save path | Not Started |
| 7 | [Result Caption Display](./07-result-caption-display.md) | `MatchStatusBadge` caption + CSS (light/dark) + grid/card wiring | Not Started |
| 8 | [Per‑Round Legend](./08-round-legend.md) | Explain the 90‑minute scoring rule once per round | Not Started |

**Suggested order:** 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8. Tasks 7 and 8 are
client‑only and depend on the data plumbed in 1–5.

## Dependencies

- [x] Tournament support (complete)
- [x] 90‑minute knockout **scoring** (`tournament-knockout-scoring`, complete)
- [x] `TournamentRoundNameParser` / `Match.ApiRoundName` (complete)
- [x] Score poller routes through `UpdateScoresForNextRoundCommandHandler` (single capture point)

## Technical Notes

### Data semantics

`ActualHomeTeamScore` / `ActualAwayTeamScore` continue to hold the **90‑minute**
score for knockouts (the scored result) — unchanged. Four new nullable columns
hold the **after‑extra‑time aggregate** (API `goals`) and the **penalty‑shootout
tally** (API `score.penalty`), captured live and finalised on `AET`/`PEN`.
Group‑stage and league matches leave all four `NULL`. Display state is derived:

* went to extra time ⇔ `AfterExtraTimeHomeScore IS NOT NULL`
* went to penalties ⇔ `PenaltyHomeScore IS NOT NULL`
* live vs final ⇔ `MatchStatus` is `InProgress` vs `Completed`

### CQRS / Dapper reminders

* Capture (write) goes through the command handler + `Match` entity, persisted by
  **`RoundRepository.UpdateMatchScoresAsync`** — whose explicit `UPDATE` column
  list must gain the four new columns (Task 4, Step 5), or nothing is saved.
* `RoundRepository` *reads*/hydrates `Match` via `SELECT m.*` (Dapper maps by
  name), so reads need no SQL change once the `Match` constructor gains the
  parameters (Task 2).
* Reads that build DTOs use `IApplicationReadDbConnection` + explicit SQL.
* **`GetRoundByIdQueryHandler` maps positionally** via a private result record —
  keep `SELECT` column order and the record constructor in lockstep (Task 5).

## Open Questions / Verification

- [ ] **Carried over from the scoring feature** — verify with API‑Football 2022
  World Cup data that `score.fulltime` is the 90‑minute score **and** `goals` is
  the after‑extra‑time aggregate for `AET`/`PEN` fixtures (some providers put
  ET‑only goals in `score.extratime`; we deliberately use `goals`). If `goals` is
  not the after‑ET aggregate, only the capture in Task 4 changes.

## Future Enhancements (Out of Scope)

- Admin manual entry/editing of ET & penalty scores (Task 6 only round‑trips
  them read‑only).
- A pulsing "live" dot on the ticker, or per‑match "Extra time" labelling
  anywhere a *prediction* is shown (never — predictions are 90′ only).
</content>
