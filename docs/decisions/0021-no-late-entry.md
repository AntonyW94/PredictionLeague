# 0021. No late entry — entries close when a season starts

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product

## Context

For a pass-required season, we need a clear cut-off for buying a pass and joining. An earlier note floated "late-join pricing" (joining mid-season at full price); the owner has confirmed that is **not** wanted.

## Decision

For pass-required seasons, **entry is not allowed once the season has started** — defined as its **first round's deadline** (the same cut-off as the refund window, 0019). After that point: no pass purchase, no trial, no joining/creating a league in that season. Before it: normal purchase/trial/join. Free seasons continue to use their existing per-league entry deadlines.

## Consequences

**For / positive**
- Simple, unambiguous rule; one cut-off shared with refunds (0019).
- No need for pro-rata/late pricing logic.
- Fair: everyone in a season started together.

**Against / cost**
- Someone who misses the start must wait for the next season — accepted.

**Neutral / notes**
- Enforced in the access gate (`SeasonEntryClosedException`) and hidden on the purchase page.
- "First round deadline" is the cut-off (not kick-off), so predictions for round 1 and entry close together.

## Alternatives considered

- **Late-join (full price, mid-season)** — rejected by the owner.
- **Pro-rata late pricing** — rejected; needless complexity for an unwanted behaviour.

## Related

- 0019 (shares the cut-off); `season-passes/08-access-gate-and-trial.md`, `10-purchase-page.md`
