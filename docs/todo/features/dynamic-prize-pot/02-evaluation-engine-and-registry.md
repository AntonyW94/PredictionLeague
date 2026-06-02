# Task: Evaluation engine & category registry

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Expose the apportionment service as an application-level **prize evaluation
engine** that produces a live breakdown DTO for any `(league, entrantCount)`, and
back it with an extensible **category registry** (kinds, default weights,
availability gates, places table).

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Application/Common/Prizes/PrizeCategoryRegistry.cs` | Create | Static/DI registry: per category → kind, default weight, availability gate, default rank table |
| `src/ThePredictions.Application/Common/Prizes/IPrizeEvaluator.cs` | Create | `PrizeBreakdownDto Evaluate(scheme, stake, entrantCount, competitionType)` |
| `src/ThePredictions.Application/Common/Prizes/PrizeEvaluator.cs` | Create | Wraps `PrizeApportionmentService`; maps to DTO; computes recommended default allocation when toggles change |
| `src/ThePredictions.Contracts/Prizes/PrizeBreakdownDto.cs` | Create | Per-category list with rank/event amounts |
| `src/ThePredictions.Contracts/Prizes/PrizeCategoryBreakdownDto.cs` | Create | Category + its slots |
| `src/ThePredictions.Contracts/Prizes/PrizeSlotDto.cs` | Create | Label + amount (+ optional delta for the +£x view) |
| `tests/Unit/...` | Create | Registry gating + evaluator mapping + recommended-allocation renormalisation |

## Implementation Steps

### Step 1: registry

- One entry per category: `Overall`, `Round`, `MostExactScores`, `Section`,
  `Monthly`, each with `Kind`, `DefaultWeight`, `AvailableFor`
  (All / SeasonsOnly / TournamentsOnly), and a default places table.
- `RecommendedAllocation(enabledCategories, stake)` → renormalise default weights
  across the enabled set and convert to **whole-pound** per-entry amounts that sum
  to the stake (largest-remainder on the renormalisation).

### Step 2: evaluator

- `Evaluate(...)` returns the full breakdown DTO at a given `N`.
- Provide `Evaluate(N)` and `Evaluate(N+1)` so callers can diff for the +£x delta
  (the diff itself lives in the surfacing task #7).

## Code Patterns to Follow

- This is read/compute only — it does **not** touch repositories; safe to use from
  query handlers (`docs/guides/cqrs-patterns.md`).
- Keep all arithmetic delegating to the Domain `PrizeApportionmentService` so the
  100%-covered maths stays in one place.

## Verification

- [ ] Unit tests: gating (Monthly hidden for tournaments, Section hidden for
      seasons); recommended allocation sums to stake for 1–5 enabled categories;
      evaluator output matches the domain service.
- [ ] Build clean with warnings-as-errors.

## Edge Cases to Consider

- All categories off (shouldn't happen — validation in task 1/4) → empty breakdown.
- Competition type unknown → default to most-restrictive gating.

## Notes

The registry is the seam for "I may add more prize types later": a new category is
one registry row (+ an `IPrizeStrategy` in task 8 if it scores differently).
