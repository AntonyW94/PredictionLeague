# 0004. Business structure & funding

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** business, financial, legal

## Context

The business needs a legal/tax structure to take payment, and a way to cover running costs (hosting, etc.) that fall **before** any income arrives — without the bank account going into overdraft. Year-one revenue is expected to be very small (~£400), roughly break-even. The owner has a day job (PAYE). The main material risk is **regulatory** (gambling), not commercial debt.

## Decision

### a) Operate as a sole trader (for launch)
Register for Self Assessment with HMRC as a **sole trader**, and **incorporate as a limited company later** when money-routing goes live (0003) or profit justifies it.

### b) Seed with owner's capital, repay from income
The owner **introduces personal money as capital** to cover initial running costs, then **repays themselves (drawings)** once Season Pass income covers them. Aim: by the following year, retained profit covers hosting without fresh income.

## Consequences

**For / positive**
- **Cost & simplicity:** free/cheap Self Assessment vs ~£500+/yr Ltd compliance that would exceed year-one revenue.
- **Early-loss relief:** sole-trader trading losses can offset the owner's day-job income; a Ltd's cannot.
- Day-job PAYE tax is unaffected.
- As a sole trader, seeding is **just "capital introduced" in / "drawings" out — no loan, no interest, no tax on the movement** (tax is only on profit). Avoids overdraft fees.

**Against / cost**
- **Unlimited personal liability** for commercial debts (judged low at this scale, with no money custody).
- Requires personal cashflow up front (interest-free float by the owner).
- A migration step (bank/Stripe in company name) when incorporating later.

**Neutral / notes**
- A Ltd would **not** shield against the main (regulatory/gambling) risk anyway — directors remain liable — so incorporation buys little protection now. Trigger to incorporate: entry-fee routing, or sustained profit.
- Record cleanly in Monzo: money in as **capital introduced**, money out as **drawings**.
- World Cup-era costs already paid personally are **not** booked to the business (0005); only business-borne costs are claimed.
- If incorporated later, seeding becomes a **director's loan** (interest-free is fine when the company owes the director), repaid without being taxable income.

## Alternatives considered

- **Limited company now** — rejected; compliance cost disproportionate to revenue, loses early-loss relief, minimal risk benefit.
- **Business overdraft** — rejected; fees, and unnecessary when the owner can float interest-free.
- **Formal interest-bearing loan to the business** — rejected; pointless for a sole trader and needless complexity.

## Related

- 0002, 0003, 0005 (free World Cup costs); `season-passes/01-business-setup-sole-trader.md`, `02-monzo-business-account.md`.
