# 0012. Recommended-price calculator rules

- **Status:** Accepted
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

The result is **always just an editable, pre-filled info box** on the create-season page — never enforced. A **small minimum floor** (at least covering Stripe fees + a little) prevents silly tiny suggestions; the admin can still edit below it.

Running costs are stored with enough detail to apportion **any way we may want in future**: **cost type, price, and start/end dates** (so proration by date-overlap is possible later). For now, when a cost is business-borne during the season it counts.

## Consequences

**For / positive**
- Fair, explainable, costs-driven pricing that scales with season length and player base.
- Respects the owner's cashflow reality (personal-until-renewal costs excluded — see 0007/0004).

**Against / cost**
- Needs accurate running-cost data and a prior comparable season for the denominator.

**Neutral / notes**
- World Cup / free seasons are excluded (run at deliberate loss with no business cost to recover — 0007).

## Resolved

- **No comparable prior season:** leave the suggestion **blank with explanatory wording**; the field is always editable, so the admin just types a price.
- **Proration:** store cost type + price + **start/end dates** so we can prorate by overlap in future; for now count a cost when business-borne during the season.
- **Minimum floor:** yes — never suggest below a small floor (covers Stripe fees + a little), still editable.
- **Comparable season** = same competition, matched on **`Season.CompetitionId`** (the `Competitions` reference table, 0018), not the API league id.

## Alternatives considered

- **Split costs evenly per season** — rejected; unfair to short seasons.
- **Flat price regardless of players** — rejected; owner wants break-even at expected (historical) player numbers.

## Related

- 0004, 0007, 0011, 0018; `season-passes/14-admin-running-costs.md`, `15-configurable-prices-and-calculator.md`
