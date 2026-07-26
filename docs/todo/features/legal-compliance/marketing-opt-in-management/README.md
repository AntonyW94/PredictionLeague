# Marketing Opt-In Management

## Status

Not Started | **In Progress (account toggle shipped; Google sign-up capture outstanding)** | Complete

> **Shipped (2026-07-25):** users can now change their marketing opt-in after registration from the
> account page (`/account/details`). The toggle is pre-populated from their current
> `MarketingOptInAtUtc` (NULL = off), and saving folds into the existing details update:
> `ApplicationUser.SetMarketingOptIn` stamps `UtcNow` when ticked and clears it when unticked. This
> makes marketing consent viewable, settable and revocable for **all** users, including Google
> sign-ups (who can opt in here).

## Summary

Lets users change their marketing opt-in choice after registration, and extends the opt-in flow to
cover Google sign-up. Required for GDPR compliance - marketing consent must be revocable, and it
must be available to all sign-up paths.

## Priority

**Medium** (the compliance-critical "revocable" part is now covered; what remains is capturing the
choice *at* Google sign-up time).

## Outstanding requirement

- [ ] **Capture the marketing choice during Google sign-up.** The Register page's marketing checkbox
      only applies to email sign-up; the Google flow currently defaults new users to `NULL` (opted
      out - a safe default). To capture the user's choice at Google sign-up, the checkbox value would
      need to survive the OAuth redirect round-trip.

## Technical Notes

- **Approach for the residual:** set a short-lived first-party cookie with the marketing choice on the
  client immediately before redirecting to Google, then read it in `ExternalAuthController` on the
  callback and pass it into `LoginWithGoogleCommand` so `CreateNewUserFromExternalLogin` records the
  chosen consent instead of the hardcoded `false`. Mind the OAuth redirect round-trip: the cookie must
  be `SameSite=Lax` (top-level navigation) to be readable on return, and it only applies to **new**
  Google users (linking an existing account must not change their consent).
- This is deliberately split from the account-toggle work: it touches the external-auth flow and
  can't be unit-tested end-to-end the way the account toggle can. It is a lower priority because the
  default (opted out) is GDPR-safe and users can opt in from the account page at any time.
- Existing column: `[AspNetUsers].[MarketingOptInAtUtc]` (datetime2, nullable). No schema change.
- Toggling off then on again loses the original opt-in date. If audit-trail granularity is ever
  needed, add a separate `MarketingOptOutAtUtc` column at that point.
