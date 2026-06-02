# Task: Domain — prize scheme & apportionment

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Add the `PrizeScheme` domain model (write-once) and a pure, fully-tested
apportionment service that turns `(scheme, pot, entrantCount)` into a
round-number prize breakdown.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/LeaguePrizeScheme.cs` | Create | Aggregate: enabled categories, per-entry £ allocation, £5-rounding threshold, write-once marker |
| `src/ThePredictions.Domain/Models/LeaguePrizeSchemeEntry.cs` | Create | One row per enabled category (`Category`, `PerEntryPounds`, optional rank-table override) |
| `src/ThePredictions.Domain/Common/Enumerations/PrizeCategoryKind.cs` | Create | `EndOfSeason`, `Recurring`, `Staged` |
| `src/ThePredictions.Domain/Services/Prizes/PrizeApportionmentService.cs` | Create | Pure apportionment: places table + £5 rounding (overall) + top-down leftover |
| `src/ThePredictions.Domain/Services/Prizes/PrizeBreakdown.cs` | Create | Result type: per-category, per-rank/per-event amounts |
| `src/ThePredictions.Domain/Models/League.cs` | Modify | Add nullable `PrizeScheme`; `SetPrizeScheme(...)` (write-once); `OverridePrizeScheme(...)` (site-admin) |
| `tests/Unit/...` | Create | 100% line + branch coverage for the above |

## Implementation Steps

### Step 1: `LeaguePrizeScheme` aggregate (write-once)

- `Create(...)` factory validates: ≥1 category enabled; per-entry allocations are
  whole pounds and **sum to the stake**; gated categories valid for the
  competition type (passed in); threshold ≥ 0.
- Immutable after creation; `League` owns the single instance and enforces
  write-once at the boundary.

### Step 2: `PrizeApportionmentService` (pure)

Implements the rules from the feature README:
- `categorySubPot = perEntryPounds × N`.
- **EndOfSeason (Overall, Exact):** apply the places threshold table for `N`;
  below the £5 threshold split at £1, above it round 2nd+ to £5 and put the odd
  £1–£4 on 1st; ranks sum exactly. A place resolving to £0 is dropped.
- **Recurring (Round/Monthly):** `floor(subPot ÷ events)` per event; remainder to
  the agreed home (final event / 1st overall — see open question).
- **Staged (Section):** 50/50 across two stages, each ranked by the ladder.
- Deterministic — **no `Random`** (unlike `PrizeDistributionHelper`); leftover is
  rank-ordered so output is reproducible and testable.

### Step 3: wire into `League`

```csharp
public void SetPrizeScheme(LeaguePrizeScheme scheme)
{
    if (PrizeScheme is not null)
        throw new InvalidOperationException("The prize scheme has already been set for this league.");
    _prizeScheme = scheme;
    HasPrizes = true;
}

// Site-admin only; authorisation enforced in the command handler, not here.
public void OverridePrizeScheme(LeaguePrizeScheme scheme) => _prizeScheme = scheme;
```

## Code Patterns to Follow

- New entities use `Entity.Create(...)` factories with validation
  (`docs/guides/domain-models.md`); tests construct via public constructor with
  explicit IDs.
- `[ExcludeFromCodeCoverage]` only on ORM-only constructors / data-only classes.
- Match `League.GetOverallRankings()` tie-handling semantics where relevant.

## Verification

- [ ] `dotnet build` clean with `/p:TreatWarningsAsErrors=true`.
- [ ] `tools\Test Coverage\coverage-unit.bat` shows **100% line & branch**.
- [ ] Apportionment unit tests cover: £5 threshold boundary, 1st-absorbs-remainder,
      place dropped at £0, ties, single-place small pots, recurring division
      remainder, section 50/50.

## Edge Cases to Consider

- Free league (stake £0) → scheme allowed but informational; no payouts.
- Per-entry allocation that leaves a category £0 at low N (place simply absent).
- Stake not divisible nicely vs. number of categories (validation message).

## Notes

This task is pure domain + the engine's maths. No DB, no API, no UI here — those
are tasks 2–8. Keeping the maths in the Domain keeps it inside the 100%-coverage
boundary.
