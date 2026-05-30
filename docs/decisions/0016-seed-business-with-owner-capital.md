# 0016. Seed the business with owner's capital, repay from income

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** business, financial

## Context

Hosting and other running costs are incurred **before** any Season Pass income arrives. The business bank account (Monzo Business) must not go into overdraft and incur fees. The owner is a **sole trader** (0004).

## Decision

The owner will **introduce personal money as capital** to cover initial running costs, then **repay themselves (drawings)** from the business account once Season Pass income covers those costs. The aim is that by the following year, retained profit covers hosting without needing fresh income.

## Consequences

**For / positive**
- Avoids overdraft fees and keeps the account solvent from day one.
- As a sole trader there is **no loan to formalise, no interest, and no tax on the money movement** — "capital introduced" in, "drawings" out. Tax is only on business profit, regardless of withdrawals.
- Running costs remain deductible expenses (feeding early-loss relief, 0004).

**Against / cost**
- Requires personal cashflow up front (an interest-free float by the owner).

**Neutral / notes**
- Record cleanly in Monzo: categorise money in as **capital introduced**, money out as **drawings**, so books distinguish own money from business income.
- World Cup-era costs already paid personally are **not** booked to the business (0007); only business-borne costs are claimed.
- If incorporated later (0004), the equivalent would be a **director's loan** (interest-free is fine when the company owes the director), repaid without it being taxable income.

## Alternatives considered

- **Business overdraft** — rejected; fees, and unnecessary when the owner can float interest-free.
- **Formal interest-bearing loan to the business** — rejected; pointless for a sole trader (no separate legal entity) and adds needless complexity.

## Related

- 0004, 0007; `season-passes/02-monzo-business-account.md`
