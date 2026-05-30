# Task: SMS Early-Bird Reward (Self-Funding Free Upgrade)

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Type

**Code (follow-on)** — needs at least one completed paid SMS season of data, so it goes live from the **second** paid season onward (not PL 2026/27).

## Goal

Reward early submitters with a **free SMS upgrade** for their next paid season — but only when it's **provably profitable**: the leftover from the SMS fee they already paid must cover the *worst case* cost of the next season's SMS.

## The Rule (profit-based — no hardcoded threshold)

For a user buying a pass for season **Y**, look at their most recent **paying** SMS-tier pass **X** (`SmsFeePaid > 0`, not already redeemed):

```
ppm              = current price per SMS message (from RunningCosts / Brevo rate, Task 04/14)
leftover_X       = X.SmsFeePaid - (X.SmsSentCount * ppm)        # profit retained on pass X
worstCase_Y      = roundsInSeason(Y) * finalWindowMilestones * ppm   # e.g. PL 38*2=76, WC 7*2=14 texts
eligible         = leftover_X >= worstCase_Y
```

If `eligible`, season Y's SMS tier is **free** (comped uplift); stamp pass X via `MarkRewardRedeemed(Y)` so its leftover can't be reused.

- The qualifying "allowance" (`X.SmsFeePaid / ppm - worstCase_messages_Y`) is therefore **computed**, and **varies by next-season length and the live per-message rate** — never a hardcoded 10.
- A **comped** season has `SmsFeePaid = 0`, so it can't fund another free season — a *paying* low-usage season earns the *next* one free (at most every-other-season free).

## Cross-Competition Eligibility (decision)

**Recommended:** a reward applies to the **next paid SMS season of any competition**, because eligibility is validated against **that season's** worst case. This is uniform and always profitable — a short season's leftover simply won't clear a long season's worst case, so short→long rarely qualifies, while long→short often will. (In practice World Cup is free, so it won't generate rewards; this mainly matters for future seasons.) See README open question if you'd prefer to restrict to the same competition.

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
- Inputs from query side (`IApplicationReadDbConnection`): the user's most recent paying, unredeemed SMS pass; that pass's `SmsFeePaid` and `SmsSentCount`; `roundsInSeason(Y)`; `ppm`.

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
- [ ] Eligibility recomputes correctly for a longer vs shorter next season.
- [ ] Domain coverage stays 100%.

## Edge Cases to Consider

- Mid-season per-message rate change: evaluate with the **current** `ppm` at purchase (conservative).
- No prior paying SMS season → not eligible (nothing has been paid to fund it).
- Reward comps **only** the SMS uplift, never the Entry fee.

## Notes

Deliberately deferred until PL 2026/27 has completed and there is real `SmsSentCount` data to evaluate.
