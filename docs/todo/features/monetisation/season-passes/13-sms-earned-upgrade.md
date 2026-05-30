# Task: SMS Early-Bird Reward (Free Upgrade)

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Type

**Code (follow-on)** — depends on at least one completed paid season of SMS data, so it goes live from the **second** paid season onward (not PL 2026/27).

## Goal

Reward disciplined early submitters: a user who was sent **fewer than a threshold (default 10) SMS** during a paid season earns a **free SMS upgrade** for their next paid season — keeping the safety net without the extra charge.

## Why

SMS only fires in the final 6 hours when predictions are still missing (Task 11). Low SMS usage therefore means the user reliably submits early. Rewarding that with free SMS next season reinforces the behaviour we want and gives genuine "peace of mind for free".

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `...Passes/Services/SmsRewardService.cs` (+ interface) | Create | Determine reward eligibility from prior-season `SmsSentCount` |
| `...Passes/Queries/GetSeasonPassPurchaseOptionsQuery(.Handler).cs` | Modify/Create | Tell the purchase page whether the SMS upgrade is free for this user |
| `09-stripe-checkout-integration.md` flow | Modify | If reward applies, grant `EntryPlusSms` while charging the Entry price |
| `10-purchase-page.md` UI | Modify | Show "You've earned free SMS for getting your predictions in early last season" |
| Config | Modify | `SmsRewardThreshold` (default 10) |

## Implementation Steps

### Step 1: Eligibility check

- `SmsRewardService.HasEarnedFreeSmsAsync(userId, newSeasonId)`:
  - Find the user's **most recent prior pass-required season** in which they held an SMS-tier pass.
  - If that pass's `SmsSentCount < SmsRewardThreshold` → **eligible**.
  - If they had no prior SMS-tier season → not eligible (nothing to reward yet).
- Query side (`IApplicationReadDbConnection`).

### Step 2: Apply the reward at purchase

- On the purchase page (Task 10), if eligible, present the **Entry + SMS** tier at the **Entry price** (the SMS uplift is comped).
- At fulfilment (Task 09 webhook), create the pass as `Tier = EntryPlusSms` with `AmountPaid` = the Entry price actually charged, and record the reward reason (`Source = RewardUpgrade` or an `SmsComped` flag — see README open question).

### Step 3: Messaging

- Purchase page banner + (optional) end-of-season recap line: "You earned free SMS reminders next season for getting your predictions in early — nice one."

## Verification

- [ ] User with prior-season `SmsSentCount < threshold` is offered free SMS; pass created as `EntryPlusSms` at Entry price.
- [ ] User at/above threshold pays the normal SMS uplift.
- [ ] User with no prior SMS season sees no reward.
- [ ] Threshold is configurable and reflected in the eligibility check.
- [ ] Domain coverage stays 100% (cover any new `SeasonPassSource`/flag).

## Edge Cases to Consider

- User who bought SMS but never had any sent (`SmsSentCount = 0`) → strongly eligible.
- User who switched between competitions: define "prior season" as the most recent *completed* pass-required season they held SMS in.
- Reward should not stack into a fully free pass — it comps **only** the SMS uplift, never the Entry fee.

## Notes

Deliberately deferred: there's no data to reward until a paid season completes. Build after PL 2026/27 has run, ahead of the next paid season's pass sales.
