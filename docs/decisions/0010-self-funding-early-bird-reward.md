# 0010. Self-funding, profit-based early-bird SMS reward

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, financial

## Context

We want to reward players who get predictions in early (and so cost us few SMS) with a free SMS upgrade next season — but only when it's genuinely affordable. An initial idea of a fixed threshold ("fewer than 10 SMS") doesn't account for varying season length or the real per-message cost.

## Decision

The reward is **profit-based and computed, not a fixed threshold**, and applies **only to the next season of the same competition**. Because the next season may not exist yet when the current one runs, the worst case is assumed to be the **same length as the current (earning) season**. For a paying SMS pass **X** (same-competition, `SmsFeePaid > 0`, not yet redeemed):

```
ppm         = current price per SMS message
leftover_X  = X.SmsFeePaid − (X.SmsSentCount × ppm)
worstCase   = X.season.rounds × finalWindowMilestones(2) × ppm   # assume next same-competition season ≈ X's length (e.g. PL 38×2=76)
eligible    = leftover_X ≥ worstCase   → next same-competition season's SMS is free
```

A comped season pays no fee so cannot fund another free one. **Final-window milestones = 2** (6h + 1h), per 0009.

## Consequences

**For / positive**
- Guaranteed never to lose money — the free upgrade is always covered by retained profit.
- Naturally varies with season length and live SMS cost (no magic number).
- Rewards and reinforces early submission.

**Against / cost**
- More logic than a flat threshold; needs the live per-message rate available at purchase.

**Neutral / notes**
- Stored on `SeasonPass`: `SmsFeePaid`, `SmsSentCount`, `RewardRedeemedForSeasonId` (no new table).
- Shown in-app as a **remaining SMS budget** to protect, not a filling bar.

## Resolved

- **Same competition only** (not any-next-season): when a season runs, the next season may not be created yet, so eligibility is computed against the **current season's own length** as the assumption for the next same-competition season.
- **"Same competition" is keyed on `Season.CompetitionId`** (the `Competitions` reference table, 0017), not the API league id — so a provider switch never invalidates an earned reward.
- **Final-window milestones = 2** (6h + 1h).

## Alternatives considered

- **Any-next-competition eligibility** — rejected; the next season's length is unknown at evaluation time, so we assume the same competition at the same length.
- **Fixed threshold (e.g. <10)** — rejected; ignores season length and real cost.
- **Filling "progress to free" bar** — rejected; each SMS works *against* the reward, so a depleting budget is the correct mental model.

## Related

- 0008, 0009, 0012, 0017; `season-passes/13-sms-earned-upgrade.md`
