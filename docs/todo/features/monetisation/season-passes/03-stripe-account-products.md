# Task: Stripe Account & Products

**Parent Feature:** [README.md](./README.md)

> **Readiness:** 🟢 Phase B — offline; **unblocked now** (Monzo Business is open). Can be started before the UTR arrives.

## Status

**Not Started** | In Progress | Complete

> **UTR note (June 2026):** You can create and activate the Stripe account **now** - a UK sole trader (Individual) account does **not** require the UTR to register or to start taking payments. Provide your legal name/DOB/address and the **Monzo Business sort code + account number** for payouts. Stripe may ask for a tax ID (UTR) but it is optional for a UK individual; add it later when it arrives in the post. Do all integration work in **test mode** first (test keys are available immediately) - live activation isn't gated on the UTR either.

## Type

**Offline** (Stripe dashboard config) — produces the keys/IDs that Task 09 consumes.

## Goal

Create a Stripe account in the business name, define one-off Season Pass Prices, enable Apple Pay / Google Pay, and configure the webhook + API keys.

## Implementation Steps

### Step 1: Create the account

- Register → https://dashboard.stripe.com/register
- **Account/business type:** Individual / **Sole trader**.

**Exact info you'll need:**

| Field | What to enter |
|-------|---------------|
| Legal name & DOB | Yours |
| Home/business address | Yours |
| Business description | *"One-off season passes for an online football prediction game"* |
| Website | `https://www.thepredictions.co.uk` |
| Bank account for payouts | Monzo Business **sort code + account number** (Task 02) |
| Statement descriptor | e.g. `THEPREDICTIONS` (what shows on customers' card statements; keep ≤22 chars) |
| Tax details | Provide UTR if asked |

### Step 2: Pricing is dynamic — no fixed Prices to pre-create

Prices are **admin-set per season** and stored in the database (`Season.PassStandardPrice` / `Season.PassPremiumPrice`, Tasks 06–07, 15). Checkout uses Stripe **dynamic `price_data`** with the amount read from the DB at session creation (Task 09) — so you do **not** create fixed Stripe Price objects by hand, and changing a season's price needs no Stripe change.

- Optionally create a single **Product** ("Season Pass") in the catalogue purely for reporting/grouping → https://dashboard.stripe.com/products .
- Currency: **GBP**. Statement descriptor as in Step 1.
- World Cup needs nothing (it's free).

### Step 3: Enable Apple Pay & Google Pay

- **Settings → Payment methods** → https://dashboard.stripe.com/settings/payment_methods — ensure **Apple Pay** and **Google Pay** are **on** (wallets are on by default for Checkout).
- Using **Stripe Checkout** (hosted), wallets render automatically:
  - **Google Pay** — no setup.
  - **Apple Pay** — Stripe handles domain verification on its hosted Checkout domain. **No Apple Developer Program membership required.** (If you later embed Payment Element on `thepredictions.co.uk`, add the domain under **Settings → Payment methods → Apple Pay → Add domain** — free; Stripe hosts the verification file.)

### Step 4: API keys & webhook

- **Developers → API keys** → copy the **test** and **live** secret keys. Store via **Azure Key Vault** (per CLAUDE.md — never in `appsettings.json`).
- **Developers → Webhooks** → add endpoint (e.g. `https://www.thepredictions.co.uk/api/stripe/webhook`), subscribe to **`checkout.session.completed`**. Copy the **signing secret** → Key Vault. (Task 09 verifies signatures with it.)

### Step 5: Note the fees

- UK standard: ~**1.5% + 20p** per successful card charge (confirm current rate in dashboard). Factor into pricing and record as a business expense.

## Verification

- [ ] Stripe account activated; payouts pointed at Monzo Business.
- [ ] Two one-off Prices created per paid competition; Price IDs recorded.
- [ ] Apple Pay + Google Pay enabled; confirmed visible on a test Checkout.
- [ ] Test + live secret keys and webhook signing secret stored in Key Vault.

## Notes

Do all integration testing in **test mode** first (Task 12). Keys for test and live are separate — wire both into Key Vault with clear naming.
