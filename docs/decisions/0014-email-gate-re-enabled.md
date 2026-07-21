# 0014. Re-enable the email-confirmation gate

- **Status:** Accepted
- **Date:** 2026-07-20
- **Deciders:** Antony
- **Tags:** product, technical, security

## Context

ADR [0009](./0009-platform-and-data.md)(b) requires a confirmed email before taking part. ADR
[0012](./0012-email-gate-temporary-suspension.md) **temporarily suspended** that gate because
verification emails were not being sent (no live Brevo template, so no new user could confirm and
therefore no new user could acquire a pass). That blocker is now gone: the "Confirm your email
address" Brevo template is live and wired (`Brevo:Templates:EmailConfirmation`), and
`RegisterCommandHandler` sends the confirmation link on registration via `EmailConfirmationSender`.

With the paid Season Pass launching, the confirmed-email requirement also matters more: it is the
main defence against farming repeated free trials with throwaway/unverified addresses (0005/0009).

## Decision

**Re-enable the email-confirmation gate on taking part**, reverting ADR 0012. An unconfirmed user is
blocked (`EmailNotConfirmedException` -> 403) at both points where participation begins:

- **`AcquireSeasonPassCommandHandler`** - the free/trial acquisition path (restores the check ADR
  0012 removed).
- **`CreateCheckoutSessionCommandHandler`** - the paid path; an unconfirmed user cannot even start
  Stripe checkout. (ADR 0012 mentioned only the acquire path because checkout did not exist yet.)

Login stays ungated (users can sign in and browse); the gate only bites at "take part", as 0012 had
it. Google-OAuth users are already confirmed and unaffected.

**Grandfather existing users**: a one-off data update sets `EmailConfirmed = 1` for all users that
predate the gate, so only new, unverified sign-ups are affected. This is run as ad-hoc SQL (a data
change, not a schema migration) on dev and, at go-live, on prod - it must run before the gated code
is live in each environment so existing users are never locked out.

## Consequences

**For / positive**
- Free-trial abuse protection is back in force for the paid launch.
- New users get a working confirmation email and a clear 403 prompt with a resend path.

**Against / cost**
- New users must confirm their email before claiming their free trial or buying - a little extra
  friction at first participation.
- Requires the one-off grandfather update in each environment (forgetting it on prod would lock out
  existing users until they confirm).

**Neutral / notes**
- The `EmailConfirmationToken` table and `AspNetUsers.EmailConfirmed` already exist (baseline
  migration); no schema change is needed.

## Related

- Supersedes 0012; implements 0009(b). See 0005 (trial abuse), `season-passes/18-email-verification-and-identity.md`.
