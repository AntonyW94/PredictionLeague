# 0002. Monetisation model & non-gambling stance

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business, legal

## Context

The site needs revenue. The user base is shaped as **few league admins, many players**. Options ranged from subscriptions to taking a cut of prize pots — but taking a cut of entry-fee-funded pots risks being classed as **pool betting** under UK gambling law. So the safest revenue is a clear **software/service** charge, and the product must stay demonstrably "a service, not gambling, and fair".

## Decision

### a) Sell one-off Season Passes
Monetise by selling a **Season Pass**: a **one-off, non-recurring** charge per competition (e.g. Premier League 2026/27) granting a player access for that season. Positioned as payment for *access to the software*, not entry to any prize. Modelled as a single `SeasonPass` entitlement (SMS is a tier on top — 0007).

### b) Whole-site gating
In a pass-required season, a pass is required to take part in **any** league — free or money. The **primary driver is legal cleanliness**: the fee is unambiguously "pay for software", fully decoupled from money-league/gambling functionality. (Simplicity and extra revenue are secondary.)

### c) No pay-to-win
**Never sell anything that confers a competitive advantage.** No purchasable boosts/power-ups. Paid features must be **convenience or cosmetic** only (e.g. SMS reminders, badges) and must never affect scoring or prediction outcomes.

## Consequences

**For / positive**
- Charges the large side of the market (players), not the few admins.
- One-off (not recurring) avoids the UK auto-renewal subscription-contract regime (DMCC Act 2024) and simplifies Stripe (`payment` mode).
- Matches the football mental model (buy each competition separately).
- Whole-site gating keeps the fee clearly "software", reinforcing the non-gambling position (0003).
- No pay-to-win protects competitive integrity and trust, and keeps monetisation on the service/cosmetic side.

**Against / cost**
- No recurring revenue; each season must be re-sold.
- A paywall (incl. whole-site gating) suppresses casual signups — mitigated by the free trial (0005) and some free seasons (0005).
- Forgoes a microtransaction line (paid boosts).

## Alternatives considered

- **Recurring subscription** — rejected; triggers auto-renewal consumer rules and feels heavier than the per-season reality.
- **Per-league service fee charged to admins** — rejected as primary; monetises the smaller side and entangles the fee with gambling-adjacent functionality.
- **% cut of entry fees / prize pots** — rejected; likely unlicensed pool betting (0003).
- **Ads/sponsorship** — deferred; negligible at current scale.
- **Gate only money leagues** — rejected; entangles revenue with the gambling-adjacent feature.
- **Paid boosts / predictive "insights" upsell** — rejected/deferred; pay-to-win. Any future "insights" must be historical/analytical only.

## Related

- 0003 (no custody / gambling stance), 0004, 0005 (pass lifecycle), 0006 (pricing), 0007 (SMS), 0008 (payments); `docs/todo/features/monetisation/season-passes/`.
