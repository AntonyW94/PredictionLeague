# Task: Stripe Account & Products

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

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

### Step 2: Create Products & Prices (one-off)

In **Product catalogue** → https://dashboard.stripe.com/products , create one Product per paid competition with **two one-off Prices** each:

| Product | Price (one-off, GBP) | Internal label |
|---------|----------------------|----------------|
| Premier League 2026/27 | £10.00 | `pl-2026-entry` |
| Premier League 2026/27 | £15.00 | `pl-2026-entry-sms` |

- Set each Price as **One time** (not recurring).
- Record each **Price ID** (`price_...`) — Task 09 maps `(SeasonId, Tier)` → Price ID (store in config, not code).
- World Cup needs no Product (it's free).

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
