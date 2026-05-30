# 0011. Admin-configurable per-season pricing via dynamic Stripe amounts

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, technical

## Context

Different seasons warrant different prices (a 38-week league vs a short cup), and prices should be tunable without code changes or hand-managing Stripe objects.

## Decision

Each pass-required season stores admin-set **`EntryPrice`** and **`SmsPrice`** (DB-backed, editable in admin). Stripe Checkout uses **dynamic `price_data`** with the amount read from the DB at session creation — **no pre-created Stripe Price objects**.

## Consequences

**For / positive**
- Prices are tunable per season in admin; changing a price needs no Stripe change and no deploy.
- No hardcoded prices anywhere (earlier plan placeholders are obsolete).

**Against / cost**
- The app is responsible for passing correct amounts; needs validation (`RequiresPass` ⇒ prices set; `SmsPrice ≥ EntryPrice`).

**Neutral / notes**
- Amounts are suggested by the calculator (0012) but always overridable.
- A single Stripe "Season Pass" Product may exist for reporting only.

## Alternatives considered

- **Pre-created fixed Stripe Prices per season/tier** — rejected; manual dashboard work per season and per price change.
- **Hardcoded prices in config/code** — rejected; not admin-tunable.

## Related

- 0002, 0008, 0012, 0014; `season-passes/03-stripe-account-products.md`, `09-stripe-checkout-integration.md`, `15-configurable-prices-and-calculator.md`
