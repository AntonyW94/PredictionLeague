# Task: Prospective & member surfacing (+£x)

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Show the live prize breakdown to current members/admins, and to prospective
members **with the +£x effect of their own entry** highlighted, before they join.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/.../Features/Leagues/Queries/GetLeaguePrizeBreakdownQuery.cs` (+Handler) | Create | Live breakdown at current N (members/admin) |
| `src/.../Features/Leagues/Queries/GetPrizePreviewQuery.cs` (+Handler) | Create | Prospective view: breakdown at N **and** N+1 → delta; no member PII |
| `src/.../Features/Prizes/Queries/EvaluateSchemeQuery.cs` (+Handler) | Create | Used by the editor preview (task 6) for an arbitrary draft allocation |
| `src/ThePredictions.Contracts/Prizes/PrizePreviewDto.cs` | Create | Breakdown + per-slot `Delta` + attribution copy |
| `src/ThePredictions.API/Controllers/LeaguesController.cs` | Modify | Endpoints for breakdown / preview / evaluate |
| `src/.../Web.Client/Components/...` | Create/Modify | Projected-prizes panel; join screen +£x rendering |

## Implementation Steps

### Step 1: queries

- Breakdown query: read pot inputs (`Price`, approved member count) + scheme via
  `IApplicationReadDbConnection`, run `IPrizeEvaluator.Evaluate(N)`.
- Preview query: evaluate at `N` and `N+1`, diff per slot → `Delta`; build copy
  ("Your £25 adds £15 to the overall prizes, £5 to section, £5 to exact").

### Step 2: prospective access (no PII)

- Returns numbers only (pot, scheme, breakdown, delta) — never member identities.
- Private leagues: gate by the **entry code** the joiner is about to use
  (they already hold it). Public leagues: open read.

### Step 3: UI

- Members/admin: "Projected prizes at N entrants — finalises at the deadline"
  panel; after the deadline show the frozen structure (existing winnings views).
- Join screen: render slots with green **+£x** deltas next to affected prizes and
  the attribution line.

## Code Patterns to Follow

- Query handlers never use repositories (`docs/guides/cqrs-patterns.md`).
- Reuse `WinningsDto` pot derivation (`EntryCount × EntryCost`) for the pot.

## Verification

- [ ] Member/admin panel matches the engine at the current count.
- [ ] Prospective preview shows correct +£x and leaks no member data.
- [ ] Private-league preview requires the entry code; public is open.
- [ ] Endpoint + handler tests; build clean.

## Edge Cases to Consider

- N=0 prospective view (first joiner sees their entry create the pot).
- Past the deadline → preview switches to the frozen breakdown, delta hidden.
- Free league → "no prizes" state.

## Notes

The +£x is the headline product goal. Show the **category-level** delta prominently
(stable) and per-rank deltas as detail (can be lumpy at £1/£5 granularity).
