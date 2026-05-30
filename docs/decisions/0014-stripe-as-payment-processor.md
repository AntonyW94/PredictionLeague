# 0014. Stripe as payment processor; Apple/Google Pay; direct charges for future Connect

- **Status:** Accepted (Checkout) / Deferred (Connect)
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** technical, business, legal

## Context

We need to take one-off Season Pass payments into the business account, support mobile wallets, and (potentially, later) route paid-league entry fees without ever holding funds (0003).

## Decision

- Use **Stripe Checkout (`payment` mode)** for one-off Season Pass purchases, paying out to the Monzo Business account.
- Rely on Stripe's built-in **Apple Pay & Google Pay** (no Apple Developer Program membership needed via Checkout; Stripe handles domain verification).
- Store keys/webhook secret in **Azure Key Vault**; fulfil passes via the **`checkout.session.completed` webhook** (idempotent), never on the redirect alone.
- **If/when** entry-fee routing is approved (0003), use **Stripe Connect direct charges** — the **league admin is the merchant of record**, funds settle to the admin's bank, the platform never holds them and only takes a defined fee.

## Consequences

**For / positive**
- Mature, UK-friendly processor; wallets out of the box; no extra Apple cost.
- Webhook-driven fulfilment is reliable against retries/closed tabs.
- Direct-charges model keeps us out of payment-institution licensing even when routing fees.

**Against / cost**
- Stripe fees (~1.5% + 20p) per charge; Connect adds per-account/payout costs (acceptable: few admins).
- Connect onboarding (KYC) per admin — deferred anyway.

**Neutral / notes**
- Amounts are dynamic from the DB (0011).

## Alternatives considered

- **PayPal / other PSPs** — not chosen; Stripe's Connect + wallet support fit best.
- **Destination charges (platform is merchant of record)** — rejected for the future Connect model; would put chargeback liability and fund-control on us. Direct charges keep the admin as merchant.

## Related

- 0002, 0003, 0011; `season-passes/03-stripe-account-products.md`, `09-stripe-checkout-integration.md`
