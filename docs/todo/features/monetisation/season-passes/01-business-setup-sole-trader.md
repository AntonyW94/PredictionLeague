# Task: Business Setup (Sole Trader)

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ⛔ Phase B — offline; HMRC sole-trader registration.

## Status

Not Started | In Progress | **Complete**

> **Update (July 2026):** Registered for Self Assessment as self-employed (sole trader) with HMRC; **UTR received** and filed. Used for Stripe live-account activation. Task closed.

## Goal

Register with HMRC as a sole trader so the business can lawfully take payment and report income, without affecting the owner's day-job (PAYE) tax.

## Type

**Offline** (no code). Do this first — Stripe and Monzo onboarding ask for your tax/business details.

## Implementation Steps

### Step 1: Confirm sole trader is the right structure

- Read **"Set up as a sole trader"** → https://www.gov.uk/set-up-sole-trader
- Chosen because at ~£400 turnover the compliance is free/simple and early-year losses can be offset against employment income. (Revisit Ltd only when paid-league money-routing goes live or profit grows.)

### Step 2: Register for Self Assessment as self-employed

- Go to → https://www.gov.uk/register-for-self-assessment/self-employed
- Sign in with (or create) a **Government Gateway** account.
- **Deadline:** register by **5 October** in the business's second tax year (i.e. the 5 October after the tax year you start trading).

**Exact info you'll need to provide:**

| Field | What to enter |
|-------|---------------|
| National Insurance number | Yours |
| Full name, DOB, home address | Yours |
| Contact email & phone | Yours |
| Date you started trading | The date you start charging (e.g. when PL 2026/27 passes go on sale) |
| Type of business / nature of work | e.g. *"Online football prediction game / software service"* |
| Trading name (optional) | *"The Predictions"* (a sole trader can trade under their own name or a business name; the name must not include "Ltd", "Limited", "plc", etc., or imply a connection with government) |
| Business address | Your home address is fine for a sole trader |

After registering you'll receive a **Unique Taxpayer Reference (UTR)** by post (keep it safe — Stripe/accounts may ask for it).

### Step 3: Set up record-keeping

- Read **"Business records if you're self-employed"** → https://www.gov.uk/self-employed-records
- Keep records of **all income** (Stripe payouts) and **all expenses** (hosting, football API, Brevo, Stripe fees, domain).
- Easiest: run everything through the Monzo Business account (Task 02) and use its built-in categorisation.

### Step 4: Understand the annual cycle (no action now)

- File **Self Assessment** online by **31 January** each year → https://www.gov.uk/log-in-file-self-assessment-tax-return
- Pay any income tax + Class 4 National Insurance on **profit** (Class 4 only above the threshold; Class 2 is no longer payable below the small-profits threshold). At ~£0 profit this is negligible.

## Day-Job Tax — Important Clarification

- Your **employment salary continues to be taxed via PAYE unchanged.** Sole-trader status does not alter your employer's deductions or the rate applied to your salary.
- Self-employment **profit** is reported separately and taxed on top at your marginal rate. A **loss** can be set against your employment income (sideways relief) for a refund.
- If you owe Self Assessment tax, HMRC may collect it through a **tax-code adjustment** — that's a collection mechanism, not extra tax.

## Verification

- [x] Registered for Self Assessment as self-employed; Government Gateway access confirmed.
- [ ] UTR received and stored securely. *(In the post - up to 28 days.)*
- [x] Record-keeping method decided (Monzo Business categorisation).

## Notes

Useful reference: https://www.gov.uk/working-for-yourself . Consider a brief chat with an accountant only if profit grows; not needed at launch scale.
