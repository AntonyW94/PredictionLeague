# Task: SMS Early-Bird Reward (Self-Funding Free Upgrade)

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Type

**Code (follow-on)** — needs at least one completed paid SMS season of data, so it goes live from the **second** paid season onward (not PL 2026/27).

## Goal

Reward early submitters with a **free SMS upgrade** for their next paid season — but only when it's **provably profitable**: the leftover from the SMS fee they already paid must cover the *worst case* cost of the next season's SMS.

## The Rule (profit-based, same-competition, same-length assumption)

A reward applies **only to the next season of the same competition**. Because that next season may not exist yet when the current one runs, the worst case **assumes it's the same length as the current (earning) season X**. For a user's most recent **paying** SMS pass **X** in that competition (`SmsFeePaid > 0`, not already redeemed):

```
ppm          = current price per SMS message (from RunningCosts / Brevo rate, Task 04/14)
leftover_X   = X.SmsFeePaid - (X.SmsSentCount * ppm)              # profit retained on pass X
worstCase    = X.season.rounds * finalWindowMilestones(2) * ppm  # assume next same-comp season ≈ X's length (PL 38*2=76)
eligible     = leftover_X >= worstCase
```

If `eligible`, the **next same-competition season's** SMS tier is **free** (comped uplift); stamp pass X via `MarkRewardRedeemed(nextSeasonId)` so its leftover can't be reused.

- The qualifying allowance is **computed** and **varies by season length and the live per-message rate** — never a hardcoded 10.
- A **comped** season has `SmsFeePaid = 0`, so it can't fund another free season — a *paying* low-usage season earns the *next same-competition* one free (at most every-other-season free).

## Why same-competition + current-length (decision — ADR 0010)

When a season runs, the next season often isn't created yet, so we can't read its real length. Restricting the reward to the **same competition** and assuming it's the **same length as the current season** lets us evaluate fairly with data we already have (X's own length, which is stable year-on-year for a given competition). A reward earned in one competition is **not** transferable to a different competition.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `...Passes/Services/ISmsRewardService.cs` (+ impl) | Create | Compute eligibility per the rule above |
| `...Passes/Queries/GetSeasonPassPurchaseOptionsQuery(.Handler).cs` | Modify/Create | Tell the purchase page whether SMS is free + the user's remaining budget |
| Task 09 fulfilment | (already wired) | Create `CreateRewardUpgrade` pass; `MarkRewardRedeemed` on funding pass |
| Task 10 purchase page | Modify | Show earned-free state + remaining SMS budget |
| Config | Modify | `FinalWindowMilestoneCount` (default 2); `ppm` source (live rate vs stored) |

## Implementation Steps

### Step 1: Eligibility service

- `SmsRewardService.EvaluateAsync(userId, seasonY)` returns `{ IsFree, FundingPassId, LeftoverGbp, WorstCaseGbp }`.
- Inputs from query side (`IApplicationReadDbConnection`): the user's most recent paying, unredeemed SMS pass **in the same competition as Y**; that pass's `SmsFeePaid`, `SmsSentCount`, and **its own season's `rounds`** (used as the same-length assumption); `ppm`. Do **not** read Y's length (it may not exist yet).

### Step 2: Apply at purchase (ties into Task 09)

- If `IsFree`, the Checkout session for the SMS tier uses `unit_amount = EntryPrice` (uplift comped); fulfilment creates `SeasonPass.CreateRewardUpgrade(...)` and calls `MarkRewardRedeemed(seasonY)` on the funding pass.

### Step 3: Display (ties into Task 10)

- For current SMS-tier holders, show **remaining SMS budget** = `SmsFeePaid - SmsSentCount * ppm`, with copy: "Keep enough in the pot to cover next season and your SMS is on us."
- On the next purchase, if earned: "You earned free SMS for getting your predictions in early — nice one."

## Storage Summary (no new tables needed)

All on `SeasonPass` (Tasks 06–07):

| Field | Role |
|-------|------|
| `SmsFeePaid` | the SMS uplift actually paid on this pass (0 if comped/trial/entry) |
| `SmsSentCount` | texts sent to the user this season |
| `RewardRedeemedForSeasonId` | set when this pass's leftover funded a later free season (prevents reuse) |

`ppm` and `FinalWindowMilestoneCount` come from config/RunningCosts, evaluated **at purchase time** — so nothing is precomputed or stale.

## Verification

- [ ] Paying low-usage pass whose leftover ≥ next season's worst case → next SMS is free; funding pass stamped redeemed.
- [ ] Leftover < worst case → user pays the normal uplift.
- [ ] Comped pass (`SmsFeePaid = 0`) never earns a further free season.
- [ ] A funding pass is used at most once (redeemed flag enforced).
- [ ] A reward earned in one competition is **not** offered when buying a different competition.
- [ ] Worst case uses the **earning season's** length (same-length assumption), not a not-yet-existing next season.
- [ ] Domain coverage stays 100%.

## Edge Cases to Consider

- Mid-season per-message rate change: evaluate with the **current** `ppm` at purchase (conservative).
- No prior paying SMS season → not eligible (nothing has been paid to fund it).
- Reward comps **only** the SMS uplift, never the Entry fee.

## Notes

Deliberately deferred until PL 2026/27 has completed and there is real `SmsSentCount` data to evaluate.
