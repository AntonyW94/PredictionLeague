# 0010. Self-funding, profit-based early-bird SMS reward

- **Status:** Accepted (some parameters open)
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, financial

## Context

We want to reward players who get predictions in early (and so cost us few SMS) with a free SMS upgrade next season — but only when it's genuinely affordable. An initial idea of a fixed threshold ("fewer than 10 SMS") doesn't account for varying season length or the real per-message cost.

## Decision

The reward is **profit-based and computed, not a fixed threshold**. When a user buys SMS for season **Y**, look at their most recent **paying** SMS pass **X**:

```
leftover_X  = X.SmsFeePaid − (X.SmsSentCount × price-per-message)
worstCase_Y = roundsInSeason(Y) × finalWindowMilestones × price-per-message   # PL 38×2=76, WC 7×2=14
eligible    = leftover_X ≥ worstCase_Y   → season Y SMS is free
```

Evaluated **at purchase time** (live rate, real next-season length). A comped season pays no fee so cannot fund another free one. **Cross-competition eligibility:** a reward applies to the **next paid SMS season of any competition**, validated against that season's worst case.

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

## Open / to confirm

- Cross-competition vs same-competition only (recommended: any next season).
- Final-window milestone count (default 2).

## Alternatives considered

- **Fixed threshold (e.g. <10)** — rejected; ignores season length and real cost.
- **Filling "progress to free" bar** — rejected; each SMS works *against* the reward, so a depleting budget is the correct mental model.

## Related

- 0008, 0009, 0012; `season-passes/13-sms-earned-upgrade.md`
