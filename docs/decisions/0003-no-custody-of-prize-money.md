# 0003. No custody of prize money (gambling / PSD2 stance)

- **Status:** Accepted (core) / Deferred (entry-fee routing)
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** legal, business, product

> Kept as a standalone record (rather than merged into 0002) because it is the single point a gambling-law solicitor will want to read in isolation.

## Context

Paid leagues have entry fees and prize pots. Two UK regulatory regimes bite if we mishandle this:
1. **Gambling** — running a prize pool funded by entrant numbers, as a business, looks like **pool betting** (licensable by the Gambling Commission).
2. **Payments** — taking possession/control of others' funds makes us a **regulated payment institution** (FCA authorisation, safeguarding, AML).

## Decision

We will **never hold, escrow, or distribute prize money** ourselves. Prize money stays a **private, peer-to-peer arrangement between league members**; the platform sells software only. Routing players' **entry fees** through the platform (even via Stripe Connect, where Stripe holds funds) is **deferred** until a **UK gambling-law solicitor signs off** the structure.

## Consequences

**For / positive**
- Avoids both the gambling-operator and payment-institution licensing burdens.
- Keeps the defensible "we're just software; members settle privately" position.

**Against / cost**
- Players settle entry fees/prizes themselves (manual); no in-app convenience for that flow.
- Forgoes potential transaction-fee revenue for now.

**Neutral / notes**
- Existing Terms §7 already states we don't hold money — kept and strengthened.
- The future Connect model, *if* approved, would use **direct charges** (admin is merchant of record), so we still never control funds (0008).

## Alternatives considered

- **Collect entry fees and pay out prizes ourselves** — rejected; unlicensed gambling + unlicensed payments.
- **Route entry fees now via Stripe Connect** — deferred; needs legal sign-off (facilitating pool betting risk).

## Related

- 0002, 0008; `season-passes/05-legal-page-updates.md`.
