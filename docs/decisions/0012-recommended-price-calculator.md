# 0012. Recommended-price calculator rules

- **Status:** Accepted (some parameters open)
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** business, financial, product

## Context

Admins set prices (0011) but need a fair, costs-based **recommendation** during season creation. The owner has confirmed the apportionment, buffer, denominator, and cost-eligibility rules. An admin **Running Costs** page records costs, renewal dates, and payer status.

## Decision

For a season being priced:

1. **Business-borne costs only** — sum annualised running costs that are business-paid as at the season start (costs still on the owner's personal card are excluded until their renewal date).
2. **Apportion by season length** (weight = this season's rounds/duration ÷ all paid seasons' length).
3. **+15% buffer.**
4. **Denominator = expected players = distinct participant count of the last completed season of the same competition** → recommend a price that **breaks even at roughly last season's player numbers**.
5. **Gross up for Stripe fees**; SMS uplift = expected SMS cost per SMS-user × buffer, grossed up.

The result is a suggestion only; the admin can override.

## Consequences

**For / positive**
- Fair, explainable, costs-driven pricing that scales with season length and player base.
- Respects the owner's cashflow reality (personal-until-renewal costs excluded — see 0007/0004).

**Against / cost**
- Needs accurate running-cost data and a prior comparable season for the denominator.

**Neutral / notes**
- World Cup / free seasons are excluded (run at deliberate loss with no business cost to recover — 0007).

## Open / to confirm

- "Comparable season" matching (same `ApiLeagueId`, else `CompetitionType`); behaviour when none exists (fall back to manual).
- Cost proration when a cost renews mid-horizon (default: include full annual if business-borne at season start).
- Minimum price floor for tiny player counts.

## Alternatives considered

- **Split costs evenly per season** — rejected; unfair to short seasons.
- **Flat price regardless of players** — rejected; owner wants break-even at expected (historical) player numbers.

## Related

- 0004, 0007, 0011; `season-passes/14-admin-running-costs.md`, `15-configurable-prices-and-calculator.md`
