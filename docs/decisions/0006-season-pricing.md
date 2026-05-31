# 0006. Season pricing: admin-configurable + recommended-price calculator

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business, financial, technical

## Context

Different seasons warrant different prices (a 38-week league vs a short cup), prices should be tunable without code changes or hand-managed Stripe objects, and admins want a fair, costs-based **recommendation** when creating a season.

## Decision

### a) Admin-configurable prices via dynamic Stripe amounts
Each pass-required season stores admin-set **`PassStandardPrice`** and **`PassPremiumPrice`** (DB-backed, editable in admin). Stripe Checkout uses **dynamic `price_data`** read from the DB at session creation — **no pre-created Stripe Price objects**. Validation: a season is either free (both prices null) or paid (both prices set, `PassStandardPrice > 0`, `PassPremiumPrice ≥ PassStandardPrice`). `RequiresPayment` is **derived** from price presence, not a stored flag.

### b) Recommended-price calculator (a suggestion, never enforced)
During season creation, show a **pre-filled, editable info box**:
1. **Business-borne costs only** — sum annualised running costs that are business-paid as at season start (personal-card costs excluded until their renewal date).
2. **Apportion by season length** (weight = this season's rounds/duration ÷ all paid seasons' length).
3. **+15% buffer.**
4. **Denominator = expected players = distinct participant count of the last completed season with the same `CompetitionId`** (0009) → break even at roughly last season's numbers.
5. **Gross up for Stripe fees**; SMS uplift = expected SMS cost per SMS-user × buffer, grossed up.
- **No comparable prior season** → leave blank with explanatory wording (still editable).
- **Minimum floor** (covers Stripe fees + a little); admin can still edit below.
- Running costs are stored with **cost type, price, and start/end dates** so proration by date-overlap is possible in future; for now a cost counts when business-borne during the season.

## Consequences

**For / positive**
- Prices tunable per season with no Stripe change/deploy; no hardcoded prices.
- Fair, explainable, costs-driven suggestion that scales with season length and player base; respects the owner's cashflow (personal-until-renewal costs excluded).

**Against / cost**
- The app must pass correct amounts (needs validation).
- Needs accurate running-cost data and a prior comparable season for the denominator.

**Neutral / notes**
- World Cup / free seasons are excluded from the calculator (deliberate loss, no business cost to recover — 0005).
- A single Stripe "Season Pass" Product may exist for reporting only.

## Alternatives considered

- **Pre-created fixed Stripe Prices / hardcoded prices** — rejected; manual dashboard work and not admin-tunable.
- **Split costs evenly per season** — rejected; unfair to short seasons.
- **Flat price regardless of players** — rejected; owner wants break-even at expected (historical) player numbers.

## Related

- 0002, 0004, 0005, 0008, 0009; `season-passes/03-stripe-account-products.md`, `09-stripe-checkout-integration.md`, `14-admin-running-costs.md`, `15-configurable-prices-and-calculator.md`.
