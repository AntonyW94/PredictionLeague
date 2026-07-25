# Feature: Season Passes - Outstanding Work (SMS / Premium tier + refunds)

## Status

**In Progress - launch shipped; SMS/Premium tier + self-service refunds outstanding**

> **The paid Standard Season Pass launch is complete and live on prod** (verified
> with a real card payment): Stripe account + product + webhook, Checkout with
> webhook-driven idempotent fulfilment, the purchase page, the acquire-first
> access gate + free/trial flow, the running-costs calculator + admin-set prices,
> the email-confirmation gate (ADR 0014), competitions management, and the
> peer-to-peer entry-fee settlement + payouts. That work has been **completed and
> its task files removed**; the decisions behind it live in ADRs 0002-0010,
> 0012, 0014, and the completed items are recorded in
> [`../../../roadmap.md`](../../../roadmap.md) under "Already Complete".
>
> **This plan now tracks only the deliberately-deferred work below.**

## What's left

| # | Task | Status | Notes |
|---|------|--------|-------|
| - | Merge the feature branch into `master` | Outstanding (one-off) | Production currently runs the feature branch; `master` must catch up or a future `master`-based deploy would overwrite prod. No new DB migrations, so the migrate step is a no-op. |
| 04 | [Brevo SMS setup](./04-brevo-sms-setup.md) | Deferred | Live SMS sending (Brevo credits). Needed before any SMS/Premium work can go live. |
| 11 | [SMS reminders](./11-sms-reminders.md) | Deferred | The additional 6h/1h SMS nudge for unsubmitted SMS-tier holders. |
| 13 | [SMS early-bird reward](./13-sms-earned-upgrade.md) | Deferred | Self-funding "next SMS season free" reward; per-user `SmsSentCount` tracking. |
| 17 | [Refunds (self-service)](./17-refunds.md) | Deferred (manual for now) | Refunds are currently handled **manually via the Stripe dashboard**; no self-service refund UI is built. Low volume expected. |

## SMS / Premium tier (the deferred feature)

The `SeasonPass` tier model is already built and in place; **Premium is simply not
offered yet** and no Premium prices are set. Re-adding it is purely additive.

**SMS reminder behaviour** (Task 11):

- **Everyone keeps every email at every milestone** (5d, 3d, 1d, 6h, 1h), exactly
  as now - including SMS subscribers.
- **SMS is additional:** at the **6h** and **1h** milestones, an SMS-tier holder
  with a valid UK mobile *also* gets a text **only if they still haven't submitted**.
  Submit before 6h and you get zero texts that round (you still got the emails).
- SMS never replaces an email; it's a bonus nudge in the final window.

**Self-funding early-bird reward** (Task 13): the SMS fee a user pays must first
cover the texts actually sent to them (`SmsSentCount x price-per-message`). If enough
of that fee is left over to cover the worst case of the next same-competition season
(`rounds x 2 final-window-milestones x price-per-message`), their **next SMS season is
free**. Begins from the **second** paid season of a competition onward. Same
competition only; evaluated against the current per-message rate; a comped season
pays no fee so cannot itself fund another free one. In-app, SMS-tier holders see
their remaining SMS budget (`fee - texts used x rate`).

**Premium pricing:** the SMS uplift reflects expected SMS cost per SMS-user over the
season (Task 04 rate) + buffer. Standard prices already exist and are admin-configurable.

## Business & Legal Context (still applies)

- **Revenue model = software access fee.** One-off, non-recurring (deliberately not
  a Stripe subscription, to stay out of the UK auto-renewal subscription-contract
  regime under the DMCC Act 2024).
- **Anti-gambling position:** the pass buys *access to the software*, not entry to a
  prize pool. Prize money in paid leagues is arranged and paid **directly between
  members**; we never collect, hold, escrow, or distribute it (ADR 0003).
- **SMS = UK mobiles only**, required and validated (libphonenumber -> E.164) at
  purchase (ADR 0007).
- **Refund policy:** passes (incl. SMS) refundable **before the season starts**,
  non-refundable after (ADR 0005, Task 17).
- **Solicitor review** of Terms & Privacy remains deferred until charging beyond
  friends & family (best-effort wording is already live).

## Technical Notes

- Follow root `CLAUDE.md`: UK English, one public type per file, `DateTime.UtcNow` +
  `Utc` suffixes, CQRS, bracketed PascalCase SQL with aliases, factory methods for
  new entities, **100% Domain coverage**, schema docs + DatabaseTools on any DB change.
- Stripe = one-off **`payment` mode** Checkout, never Billing/Subscriptions.
- Store only a Stripe **payment reference** on `SeasonPass` - never card data.

## Related / Future Features

- **[Season Challenges (badges)](../season-challenges/)** - the general badges engine
  already ships (`user-experience` achievements-badges, now complete); the outstanding
  piece there is gating badges to a paid Season Pass.

## Open Questions

- [ ] **Reward display detail** - show remaining SMS budget as GBP or as a message
  count, and where it appears (dashboard / pass page).
- [ ] Whether to add a distinct `SeasonPassSource.RewardUpgrade` value, or keep the
  `SmsFeePaid = 0` + `RewardRedeemedForSeasonId` modelling (Task 13's default).
