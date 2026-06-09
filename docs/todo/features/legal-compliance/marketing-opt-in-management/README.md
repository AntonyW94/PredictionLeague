# Marketing Opt-In Management

## Status

Not Started | **In Progress** | Complete

> **Verified June 2026:** Foundation only. `ApplicationUser.MarketingOptInAtUtc` and registration-time consent capture (`RecordRegistrationConsent`) exist. Outstanding: a post-registration opt-in/out toggle on the account page, an update endpoint, and marketing-consent wiring for Google sign-up.

## Summary

Lets users change their marketing opt-in choice after registration, and extends the opt-in flow to cover Google sign-up. Required for GDPR compliance — marketing consent must be revocable, and it must be available to all sign-up paths, not just email.

## Priority

**High** (compliance gap before onboarding outside friends-and-family)

## Requirements

- [ ] Marketing opt-in toggle on the Account Settings page (`/account/details`) using the same button-style toggle as Register
- [ ] API endpoint to update `MarketingOptInAtUtc` (set to `UtcNow` when ticked, `NULL` when unticked)
- [ ] Pre-populate the toggle from the user's current `MarketingOptInAtUtc` value (NULL = off)
- [ ] Wire the marketing checkbox on the Register page through to Google sign-up (currently it only applies to email sign-up — Google flow always defaults to NULL). Likely via cookie set before redirect, read by `LoginWithGoogleCommandHandler` on callback.

## Technical Notes

- Existing column: `[AspNetUsers].[MarketingOptInAtUtc]` (datetime2, nullable). No schema change needed.
- Existing domain hook: `ApplicationUser.RecordRegistrationConsent` already toggles the column. A separate `SetMarketingOptIn(bool, DateTime)` method is the cleanest extension for the post-registration toggle so the registration-only `RecordRegistrationConsent` keeps a tight scope.
- This loses the original opt-in date if a user toggles off then on again. If audit-trail granularity is ever needed, add a separate `MarketingOptOutAtUtc` column at that point.
