# Task: Configurable Season Prices & Recommended-Price Calculator

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ✅ Phase A — buildable now (no accounts; uses fee constants).

## Status

**Not Started** | In Progress | Complete

## Goal

Let the admin set each pass-required season's **Entry** and **Entry + SMS** prices, and show a **recommended price** during season creation, computed from the running costs (Task 14) using the owner-confirmed rules.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/Season.cs` | Modify | `EntryPrice` / `SmsPrice` (Task 06) |
| Seasons columns | Modify | (Task 07) |
| `...Application/Features/.../PriceRecommendation/IPriceRecommendationService.cs` (+ impl) | Create | The calculator |
| `...Queries/GetSeasonPriceRecommendationQuery(.Handler).cs` | Create | Feed the season-create UI |
| Admin season create/edit page + commands | Modify | Price fields + "Recommended £X" with override |
| Task 09 Checkout | (already wired) | Uses `EntryPrice`/`SmsPrice` as dynamic `price_data` |

## Calculator Algorithm (owner-confirmed decisions)

For a new season **S** being priced:

1. **Business-borne costs** (Task 14): sum `AnnualisedAmount` of every cost where `IsBusinessBorneOn(S.start)` — i.e. exclude costs still on the personal card until they renew.
2. **Apportion by season length:** `weight(S) = length(S) / Σ length(all paid seasons in the horizon)`, where length = number of rounds (or duration). `seasonCosts = businessBorneAnnualCosts × weight(S)`. (Free seasons are excluded — World Cup runs at a deliberate loss.)
3. **Buffer:** `target = seasonCosts × 1.15` (15%).
4. **Expected players = break-even denominator:** the **distinct approved participant count of the last completed season with the same `CompetitionId`** (the `Competitions` reference table — ADR 0009, not `ApiLeagueId`). `perPlayer = target / expectedPlayers`.
5. **Gross up for Stripe fees:** `entryRecommendation = (perPlayer + stripeFixedFee) / (1 − stripePercent)`, rounded to a tidy figure (e.g. nearest £0.50/£1). → suggested **Entry price**.
6. **SMS uplift:** expected SMS cost per SMS-user over the season ≈ `expectedSmsPerUser × ppm`, × 1.15, grossed up → add to Entry → suggested **Entry + SMS price**. (`ppm` from Task 04/14; `expectedSmsPerUser` an admin-tunable assumption, default a fraction of the worst case `rounds × finalWindowMilestones`.)

Result surfaced as *"Recommended: £X Entry · £Y +SMS (breaks even at ~N players)"*. **Admin can override**; the stored values are what Stripe charges.

## Implementation Steps

### Step 1: Season price fields + validation

- `EntryPrice` / `SmsPrice` on `Season` (Task 06): required & `> 0` when `RequiresPass`, `SmsPrice >= EntryPrice`; null when free.

### Step 2: Calculator service

- `IPriceRecommendationService.RecommendAsync(seasonDraft)` implementing the algorithm. Pure-ish/Application-layer; reads costs + prior-season participant counts via the read connection.

### Step 3: Season create/edit UI

- The recommendation is **always just an editable, pre-filled info box** with a breakdown (apportioned costs, buffer, expected players, fees) so the figure is explainable — never enforced.
- **No comparable prior season → leave the price blank with explanatory wording** (e.g. "Not enough history to suggest a price yet — set one manually"); the field stays editable.
- Apply a **small minimum floor** (covers Stripe fees + a little) to the suggestion; the admin can still type a lower value.
- Editable Entry / Entry + SMS fields, defaulted to the recommendation, saved on the season.

## Verification

- [ ] Recommendation matches a hand-worked example for given costs/players/rounds.
- [ ] Personal-until-renewal costs are excluded before their renewal date.
- [ ] Longer seasons get a larger cost share than shorter ones.
- [ ] Admin override persists and is what Stripe charges.
- [ ] Free seasons show no calculator/price.
- [ ] Domain coverage 100% for any new `Season` validation.

## Edge Cases to Consider

- **No prior comparable season** (first ever of a competition): leave the suggestion **blank with explanatory wording**; the field is editable so the admin just types a price.
- **Cost proration:** running costs store **start/end dates** (Task 14), so the calculator *can* prorate by date-overlap in future; for now include the cost when it's business-borne during the season.
- **Minimum floor** applied so high player counts don't produce an absurdly tiny suggestion.
- "Comparable season" = same competition, matched on `Season.CompetitionId` (the `Competitions` table, ADR 0009).

## Notes

This task makes the README "placeholder prices" obsolete — nothing is hardcoded; the admin sets prices, guided by the calculator.
