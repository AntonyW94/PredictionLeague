# Task: Testing & Launch

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ⛔ Phase B — needs live Stripe + Brevo and solicitor sign-off.

## Status

**Not Started** | In Progress | Complete

## Goal

Configure seasons, validate the whole flow in Stripe test mode, then go live for Season Passes — with paid-league money-routing deliberately left OFF.

## Implementation Steps

### Step 1: Season configuration

- All existing seasons + **World Cup 2026**: `RequiresPass = false` (free).
- **Premier League 2026/27**: `RequiresPass = true`.
- Map PL 2026/27 tiers to the Stripe Price IDs from Task 03.

### Step 2: End-to-end test (Stripe TEST mode)

| Scenario | Expected |
|----------|----------|
| Brand-new user joins a PL league | Free trial granted; can join; no Checkout |
| World-Cup-only user joins PL | Blocked → purchase page → pays (test card) → pass created → can join |
| Buy Entry + SMS | Pass tier = `EntryPlusSms` |
| SMS-tier user, submits early (>6h before) | Gets all emails (incl. 6h/1h); **no SMS**; `SmsSentCount` unchanged |
| SMS-tier user, still unsubmitted at 6h/1h | Gets the email **plus** an extra SMS; `SmsSentCount` +1 each milestone |
| Apple/Google Pay | Wallet buttons appear in test Checkout |
| Webhook retry / double event | Only one pass created (idempotent) |
| Existing past-season user joins PL | Must purchase |
| Free season join | No pass needed, unchanged |

- Use Stripe test cards → https://docs.stripe.com/testing
- Verify reminder job sends SMS to SMS-tier holders and email to others.

### Step 3: Pre-launch checks

- [ ] Legal pages updated **and solicitor-reviewed** (Task 05).
- [ ] `database-schema.md` + DatabaseTools updated (Task 07).
- [ ] Domain coverage 100% (`coverage-unit.bat`).
- [ ] Live Stripe keys + webhook secret in Key Vault; live webhook endpoint registered.

### Step 4: Go live

- Switch Stripe to **live** keys.
- Announce PL 2026/27 passes.
- **Keep paid-league entry-fee routing (Stripe Connect) OFF** — that remains gated on the gambling-solicitor opinion (separate future feature).

## Verification

- [ ] All test scenarios pass in test mode.
- [ ] First real (live) purchase creates a pass and pays out to Monzo Business.
- [ ] No regression to free seasons / existing users.

## Edge Cases to Consider

- Time-zone correctness on reminder sends (`DateTime.UtcNow`).
- Refunds: handle manually via Stripe dashboard per the refund clause (Task 05) for v1.

## Notes

This feature monetises **software access only**. Routing players' prize money is explicitly out of scope until legal sign-off.
