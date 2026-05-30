# 0007. World Cup 2026 free; existing seasons grandfathered; later seasons paid

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business, financial

## Context

The paid model is being introduced onto an existing free site. We must decide which seasons require a pass. The `Season` entity gains a `RequiresPass` flag (default `false`).

## Decision

- All **existing/past seasons** stay free (grandfathered; `RequiresPass = false`).
- **World Cup 2026** is **free for everyone** (`RequiresPass = false`).
- **Premier League 2026/27 onward** require a pass (`RequiresPass = true`).

## Consequences

**For / positive**
- **World Cup costs are already paid from the owner's personal account and will never be booked as a business expense** — so there is *nothing for the business to recover*, making "free" the natural choice (it is not a business loss).
- Acts as a free launch/transition period that builds goodwill before charging.
- Default-`false` flag means grandfathering is automatic and risk-free for existing data.

**Against / cost**
- A short window of running the site with no business revenue (but no business cost either, per above).

**Neutral / notes**
- After the World Cup, **all seasons are paid**, so no ongoing free-season subsidy logic is needed in the price calculator (0012).
- Free-season participation still consumes the first-timer trial (0006).

## Alternatives considered

- **Charge for the World Cup too** — rejected; its costs are personal/sunk and won't be charged to the business, so there's nothing to recoup, and a free tournament builds the base.
- **Keep a permanent free tier** — rejected; conflicts with whole-site gating (0005) going forward.

## Related

- 0005, 0006, 0012; `season-passes/07-database-migration.md`, `12-testing-and-launch.md`
