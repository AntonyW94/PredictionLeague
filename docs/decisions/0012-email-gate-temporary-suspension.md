# 0012. Temporarily suspend the email-confirmation gate

- **Status:** Superseded by [0014](./0014-email-gate-re-enabled.md) (the gate was re-enabled once verification emails shipped)
- **Date:** 2026-06-05
- **Deciders:** Antony
- **Tags:** product, technical, security

## Context

ADR [0009](./0009-platform-and-data.md)(b) decided to **require a confirmed email before taking part**. Only part of that flow is built: `AcquireSeasonPassCommandHandler` rejects an unconfirmed user with `EmailNotConfirmedException`, but the rest of verification — a working send-on-register, a confirm endpoint wired end-to-end, and the Brevo template — is **not finished** (see `season-passes/18-email-verification-and-identity.md`).

The effect in production today: verification emails are not being sent, so **no new user can confirm their email**, which means **no new user can acquire their (free) season pass** — and without a pass they can't join or create a league. New sign-ups are effectively locked out of the product. Login itself is not gated, so users can sign in and browse, but they stall at the season-pass step.

## Decision

**Temporarily remove the email-confirmation gate on season-pass acquisition.** `AcquireSeasonPassCommandHandler` no longer throws `EmailNotConfirmedException`; an unconfirmed user can acquire a free/trial pass and take part.

This is a **temporary** reversal of ADR 0009(b)'s gate only. The rest of 0009 (Competitions table, normalised email identity) is unaffected. The `EmailNotConfirmedException` type and its `ErrorHandlingMiddleware` → 403 mapping are **kept in place** so the gate can be re-enabled by restoring a single check once verification emails work.

## Consequences

**For / positive**
- New users can complete onboarding and use the site immediately, unblocking sign-ups.

**Against / cost**
- The free-trial abuse protection that a verified email provides (0005/0009) is **not in force** until re-enabled — a user could farm free passes with unverified addresses. Email **normalisation** (plus-suffix stripping) still applies as a partial mitigation.
- This is a security/anti-abuse regression accepted only as a stopgap.

**Neutral / notes**
- Re-enabling is a one-line change in `AcquireSeasonPassCommandHandler` plus restoring its test. Google-OAuth users are already confirmed and unaffected either way.

## Alternatives considered

- **Leave the gate and ship the email template first** — rejected for now; unblocking live sign-ups is the immediate priority and the template isn't ready.
- **Auto-grant a pass on registration** — rejected; acquisition stays a deliberate onboarding step, and this would entrench bypassing verification more deeply than a single suspended check.

## Related

- 0009 (amended — email gate suspended), 0005; `monetisation/season-passes/18-email-verification-and-identity.md`.
