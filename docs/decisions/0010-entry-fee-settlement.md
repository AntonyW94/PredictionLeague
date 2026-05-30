# 0010. Entry-fee settlement — admin bank details (encrypted) & join/pay flow

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, security, legal

## Context

Per 0003 the platform never custodies prize money — members settle **peer-to-peer**. To make that smooth in-app (instead of players DM-ing the admin for bank details), a league admin can store their bank details against a league, shown to joining players with the amount and a reference. Bank details are personal data and must be protected. We also need to decide how the join/payment gate works.

## Decision

### a) Admin bank details at league creation, encrypted at rest
A league admin may add **bank details** (account name, sort code, account number, optional reference template) when creating/editing a league. They are **stored encrypted at rest** (app-level AES; key in **Azure Key Vault**; ciphertext in the DB), **decrypted server-side only for authorised viewers** — the admin themselves and players who have **requested/joined that league** (for payment). Never written to logs, never returned to non-members, scrubbed in dev DB refreshes.

### b) Join/pay flow (UX)
**Share the entry code freely.** A player **requests to join** and is immediately shown the admin's **payment details + exact amount + unique reference**. The **admin accepts once payment is received** — acceptance stays admin-controlled (an optional player "I've paid" button only *nudges*; it never auto-accepts). The software facilitates **information and tracking only, never the money** (0003). Payment details are shown only to pending/approved members of that league, never publicly.

## Consequences

**For / positive**
- Self-service join with a clear payment call-to-action at the point of intent; far less manual back-and-forth than "pay first, then I hand out the code".
- Admin keeps the accept-gate (moved from controlling the code to confirming payment).
- Encryption + least-exposure display protect sensitive personal data.

**Against / cost**
- Anyone with the code can see the admin's bank details (low-sensitivity for *receiving*; mitigated by showing only to requesters).
- Admin still manually reconciles/accepts payments.
- Storing bank details adds a security surface — mitigated by encryption, access control, no-logging, and dev-refresh anonymisation.

**Neutral / notes**
- Admin can still keep the code semi-private within their group if preferred — the flow works either way.
- If an admin sets no bank details, fall back to the current "arrange payment manually" behaviour.

## Alternatives considered

- **Keep "pay first → then code"** — rejected; gatekept code + manual DMs, poor UX.
- **Auto-accept on the player's "I've paid"** — rejected; no payment verification, opens abuse.
- **Store bank details in plaintext** — rejected; sensitive personal data must be encrypted at rest.
- **Route the payment through the app (Stripe/Monzo links)** — rejected; entry-fee/stake routing breaches Stripe's AUP and the no-custody stance (0003, 0007).

## Related

- 0003 (no custody); Task 19 (and Tasks 08 join flow, 07 DB, plus DatabaseTools anonymisation).
