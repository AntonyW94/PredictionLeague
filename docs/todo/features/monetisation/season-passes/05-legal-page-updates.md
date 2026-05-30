# Task: Legal Page Updates

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ✅ Phase A — draft the edits now; **solicitor review is a Phase B go-live gate**.

## Status

**Not Started** | In Progress | Complete

## Goal

Update Terms of Service and Privacy Policy to reflect a paid service (Season Passes), Stripe as a processor, SMS reminders, and consumer/refund rights — while preserving the anti-gambling "we don't hold prize money" position. **Flag for solicitor review before go-live.**

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Web.Client/Components/Pages/Legal/TermsOfService.razor` | Modify | Paid-service wording, refund/consumer-rights clause, strengthen private-league clause |
| `src/ThePredictions.Web.Client/Components/Pages/Legal/PrivacyPolicy.razor` | Modify | Add Stripe processor, payment data, SMS lawful basis, retention |

## Implementation Steps

### Step 1: Terms of Service

- **§2 "The Service":** remove *"provided free of charge and we do not process any payments."* Replace with:
  > Taking part in some seasons requires a paid **Season Pass**, which grants access to that season's leagues and features. A Season Pass is a one-off charge for access to the Service — it is **not** a stake, bet, or entry into any prize fund, and it does **not** renew automatically. Some seasons are free, and first-time players may receive their first Season Pass free.
- **§7 "Private leagues":** keep, and append:
  > Where a private league has an entry fee or prize, that money is arranged and paid **directly between the league's members**. The Predictions does not collect, hold, escrow, or distribute it, and is not a gambling operator.
- **§10:** remove *"free of charge as a hobby project"*; reframe as a paid service (keep the no-guaranteed-uptime spirit within consumer-law limits).
- **§11:** add: *"Nothing in these terms affects your statutory rights as a consumer under the Consumer Rights Act 2015."*
- **NEW section "Season Passes, payments and refunds":**
  > Season Passes are one-off, non-recurring charges processed by **Stripe**. You may **cancel for a full refund any time before the season starts** (its first round deadline); after the season has started a pass is non-refundable, and by taking part you consent to immediate access and acknowledge you lose the statutory 14-day cancellation right. The SMS reminder option follows the same rule (refundable before the season starts, non-refundable after); pausing reminders does not itself entitle you to a refund. If we ever cancel a season, affected paid players are refunded. (See ADR 0005.)
- Bump `LastUpdated`. Keep the `@* solicitor review *@` TODO comment.

### Step 2: Privacy Policy

- **§2 "What we collect":** phone number → *"required if you choose the SMS reminder option"*; add **payment data** (note Stripe handles card details; we store only a Stripe payment/customer reference, never card numbers).
- **§3 "Lawful basis":** add **SMS deadline reminders** = *performance of a contract* (a paid service), not marketing consent; **payment processing** = performance of a contract + legal obligation (tax records).
- **§4 "Who we share with":** **add Stripe** ("Payment processing — Stripe Payments Europe"); note Brevo now also sends **SMS**.
- **§6 "How long we keep it":** add *payment/transaction records kept ~6 years for UK tax/accounting law*.
- Bump `LastUpdated`. Keep the `@* solicitor review *@` TODO comment.

## Verification

- [ ] Terms no longer claim the service is free / payment-free.
- [ ] Private-league anti-gambling clause present and strengthened.
- [ ] Refund/consumer-rights + Stripe wording added to Terms.
- [ ] Privacy lists Stripe, payment data, SMS basis, 6-year retention.
- [ ] `LastUpdated` bumped on both; solicitor-review TODO retained.
- [ ] Both pages render correctly in light and dark mode.

## Edge Cases to Consider

- Trial users pay nothing — wording must not imply everyone pays.
- World Cup / free seasons must still be described as free.

## Notes

This task has **no DB impact** and is the lowest-risk place to start. Final wording is subject to solicitor review.
