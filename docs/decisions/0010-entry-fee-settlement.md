# 0010. Peer-to-peer money settlement (entry fees & payouts)

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

### c) Player payout details (optional, encrypted, admin-only)
A player may **optionally** store **payout bank details** so league admins can pay winnings without messaging each winner. Stored **encrypted at rest** and **visible only to the admin(s) of leagues the player is a member of** — the UI **names which admin(s) can see them** so consent is explicit, and the player can delete them. If a player **doesn't** provide them and wins, the admin must contact them manually (made clear up front).

### d) Payouts list (manual mark-as-paid)
At league end the admin sees a **payouts list** — each winner, amount, and (if shared) their payout details — and **marks each as paid** one by one (`Winnings.PaidAtUtc`). Winners with no stored details show a "contact them" prompt. Settlement is **manual/peer-to-peer**; this could be replaced by an automated payout system later **only with legal sign-off** (0003/0008).

## Consequences

**For / positive**
- Self-service join with a clear payment call-to-action at the point of intent; far less manual back-and-forth than "pay first, then I hand out the code".
- Admin keeps the accept-gate (moved from controlling the code to confirming payment).
- Payouts: admins pay winners without chasing each for details; the payouts list + mark-as-paid gives a clean record and a future automation seam.
- Encryption + least-exposure display protect sensitive personal data (admin receiving details *and* player payout details).

**Against / cost**
- Anyone with the code can see the admin's bank details (low-sensitivity for *receiving*; mitigated by showing only to requesters).
- Admin still manually reconciles/accepts payments and marks payouts.
- Storing bank details (both sides) adds a security/PII surface — mitigated by encryption, strict access control, no-logging, dev-refresh anonymisation, optional + deletable player details, and GDPR purpose-limitation/retention (keep only while needed for payouts).

**Neutral / notes**
- Admin can still keep the code semi-private within their group if preferred — the flow works either way.
- If an admin sets no bank details, fall back to the current "arrange payment manually" behaviour.

## Alternatives considered

- **Keep "pay first → then code"** — rejected; gatekept code + manual DMs, poor UX.
- **Auto-accept on the player's "I've paid"** — rejected; no payment verification, opens abuse.
- **Store bank details in plaintext** — rejected; sensitive personal data must be encrypted at rest.
- **Route the payment through the app (Stripe/Monzo links)** — rejected; entry-fee/stake routing breaches Stripe's AUP and the no-custody stance (0003, 0007).

**Neutral / notes**
- Player payout details are profile-level but **access is granted per league** (only admins of leagues the player is in can decrypt them); the player sees the named admin(s) with access.

## Related

- 0003 (no custody), 0008 (future automated payouts); Tasks 19 (entry fees), 20 (payouts), 07 (DB), plus DatabaseTools anonymisation.
