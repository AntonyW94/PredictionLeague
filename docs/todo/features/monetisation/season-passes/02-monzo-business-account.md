# Task: Monzo Business Account

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ✅ Phase B — offline; done.

## Status

Not Started | In Progress | **Complete**

> **Update (June 2026):** Monzo Business account applied for and **open** in the business name. This unblocks Stripe (Task 03), which uses the Monzo sort code + account number for payouts. Remaining items below (tax pot, transaction categories) are optional bookkeeping housekeeping, not blockers.

## Type

**Offline** (no code).

## Goal

Open a free Monzo Business Lite account in the business name to receive Stripe payouts and keep business finances separate.

## Implementation Steps

### Step 1: Apply for Monzo Business Lite (free)

- Start here → https://monzo.com/business (choose **Lite — £0/month**)
- Plan comparison → https://monzo.com/business-banking/plans-pricing
- Application is via the **Monzo app** (you may need a personal Monzo identity verified first).

**Exact info you'll need:**

| Field | What to enter |
|-------|---------------|
| Business type | **Sole trader** |
| Trading name | *"The Predictions"* (or your own name) |
| Nature of business | *"Online games / software service"* |
| Estimated annual turnover | Realistic estimate (e.g. £400–1,000 for year one) |
| Your personal details | Name, DOB, address, NI number, photo ID for verification |
| UTR | From Task 01 if requested |

### Step 2: Set up the account for bookkeeping

- Create a **Tax pot** (manual on Lite) and move aside a % of each payout for any tax due.
- Turn on transaction **categories** and **receipt attachments** — this is your Self Assessment evidence.
- Note the account **sort code** and **account number** — you'll enter these into Stripe (Task 03) for payouts.

### Step 3: Know the free-plan limits

- Lite is **single-user**, **no invoicing** (Pro £9/mo adds invoicing + Xero/QuickBooks feeds). Not needed for one-off season passes.
- Built-in **MTD/HMRC tools** are included on Lite for filing.

## Verification

- [x] Monzo Business Lite account open in the business name.
- [ ] Sort code + account number recorded (for Stripe payout setup). *(To hand when creating the Stripe account.)*
- [ ] Tax pot + categorisation enabled. *(Optional bookkeeping; not a blocker.)*

## Notes

Personal Monzo Max does **not** include Business Pro — Lite is the right (free) choice here.
