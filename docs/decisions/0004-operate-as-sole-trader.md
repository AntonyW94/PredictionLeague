# 0004. Operate as a sole trader for launch

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** business, financial, legal

## Context

The business needs a legal/tax structure to take payment. Year-one revenue is expected to be very small (~£400) with roughly break-even economics. The owner has a day job (PAYE income). The main material risk is **regulatory** (gambling), not commercial debt.

## Decision

We will **operate as a sole trader** for launch, registering for Self Assessment with HMRC, and **incorporate as a limited company later** when money-routing goes live (see 0003) or profit justifies it.

## Consequences

**For / positive**
- **Cost & simplicity:** free/cheap Self Assessment vs ~£500+/yr Ltd compliance that would exceed year-one revenue.
- **Early-loss relief:** sole-trader trading losses can be set against the owner's day-job income; a Ltd's losses cannot.
- Day-job PAYE tax is unaffected by becoming a sole trader.

**Against / cost**
- **Unlimited personal liability** for commercial debts (judged low at this scale, with no money custody).
- Will incur a migration step (bank/Stripe in company name) when incorporating later.

**Neutral / notes**
- A Ltd would **not** shield against the main (regulatory/gambling) risk anyway — directors remain liable — so incorporation buys little protection now.
- Trigger to incorporate: enabling entry-fee routing, or sustained profit.

## Alternatives considered

- **Limited company now** — rejected; compliance cost disproportionate to revenue, loses early-loss relief, minimal risk benefit at this stage.

## Related

- 0002, 0003; `season-passes/01-business-setup-sole-trader.md`, `02-monzo-business-account.md`
