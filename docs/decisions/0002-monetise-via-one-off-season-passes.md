# 0002. Monetise via one-off Season Passes

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business, legal

## Context

The site needs revenue. The user base is shaped as **few league admins, many players**. Options ranged from subscriptions to taking a cut of prize pots. Taking a cut of entry-fee-funded prize pots risks being classed as **pool betting** under UK gambling law, so the safest revenue is a clear **software/service** charge.

## Decision

We will monetise by selling a **Season Pass**: a **one-off, non-recurring** charge per competition (e.g. Premier League 2026/27) that grants a player access for that season. It is positioned as payment for *access to the software*, not entry to any prize.

## Consequences

**For / positive**
- Charges the large side of the market (players), not the few admins.
- Clean re: gambling law — it's a service fee, not a stake.
- One-off (not a recurring subscription) avoids the UK auto-renewal subscription-contract regime (DMCC Act 2024) and simplifies Stripe (Checkout `payment` mode).
- Matches the football mental model (buy each competition separately).

**Against / cost**
- No recurring revenue; each season must be re-sold.
- A paywall can suppress casual signups (mitigated by the free trial — see 0006).

**Neutral / notes**
- Modelled as a single `SeasonPass` entitlement; SMS is a tier on top (see 0008).

## Alternatives considered

- **Recurring subscription** — rejected; triggers auto-renewal consumer rules and feels heavier than the per-season reality.
- **Per-league service fee charged to admins** — rejected as primary; monetises the smaller side and entangles the fee with money-league/gambling functionality.
- **% cut of entry fees / prize pots** — rejected (now); likely unlicensed pool betting (see 0003).
- **Ads/sponsorship** — deferred; negligible at current scale.

## Related

- `docs/todo/features/monetisation/season-passes/`
- 0003, 0005, 0006, 0008, 0011, 0014
