# Feature: Season Passes (Website Monetisation)

## Status

**Not Started** | In Progress | Complete

## Summary

Introduce a paid, one-off **Season Pass** that gates participation in pass-required seasons (e.g. Premier League 2026/27). A pass is a **one-off charge for access to the software/service** — explicitly **not** a stake, bet, or entry into any prize fund — sold in two tiers: **Entry** and **Entry + SMS reminders**. Free seasons (e.g. World Cup 2026) and all existing seasons stay free. **First-time players get their first season free** as a trial. The Predictions never holds prize money; paid-league entry fees remain a private, peer-to-peer matter between members.

## User Story

As a player, I want to buy a one-off Season Pass for a competition so that I can take part in its leagues for the whole season (with optional SMS deadline reminders) — and as a brand-new player, I want my first season free so I can try before I pay.

## Business & Legal Context (READ FIRST)

- **Revenue model = software access fee.** One-off, **non-recurring** (deliberately not a Stripe subscription, to stay out of the UK auto-renewal subscription-contract regime under the Digital Markets, Competition and Consumers Act 2024).
- **Anti-gambling position:** the pass buys *access to the software*, not entry to a prize pool. Prize money in paid leagues is arranged and paid **directly between members**; we never collect, hold, escrow, or distribute it. Terms §7 already states this — keep and strengthen it.
- **OUT OF SCOPE (gated on legal advice):** routing paid-league **entry fees** through the platform via Stripe Connect. That is a separate, later phase requiring a **UK gambling-law solicitor's sign-off**. Nothing in this feature touches prize money.
- **Solicitor review** of the updated Terms & Privacy is required before go-live (both pages already carry a "solicitor review" TODO).
- **Business structure:** sole trader (see Task 01). Self-employment does not change PAYE tax on the owner's day job.

## Design / Mockup

External mockup: `season-pass-mockup.html` (delivered separately). Two pricing cards with a season toggle.

```
┌──────────────────────────────────────────────────────────┐
│                  Get your Season Pass                     │
│      [ Premier League 2026/27 ] [ World Cup 2026 ]        │
│                                                           │
│  ┌─────────────────────┐   ┌─────────────────────────┐    │
│  │  Season Entry       │   │  Season Entry + SMS  ★   │    │
│  │  £10  one-off       │   │  £15  one-off            │    │
│  │  ✓ Join all leagues │   │  ✓ Everything in Entry   │    │
│  │  ✓ Predictions      │   │  ✦ SMS deadline reminders│    │
│  │  ✓ Leaderboards     │   │                          │    │
│  │  ✓ Email reminders  │   │                          │    │
│  │  [ Get Entry ]      │   │  [ Get Entry + SMS ]     │    │
│  └─────────────────────┘   └─────────────────────────┘    │
│   🔒 Secure payment via Stripe · one-off, no auto-renewal  │
└──────────────────────────────────────────────────────────┘
```

Trial-eligible (brand-new) users see a "Your first season is on us" state instead of prices.

## Pricing (placeholder — finalise after costing in Task 03/04)

| Competition | Entry | Entry + SMS | Notes |
|-------------|-------|-------------|-------|
| Premier League 2026/27 | £10 | £15 | `RequiresPass = true` |
| World Cup 2026 | Free | n/a | `RequiresPass = false` (free for everyone) |
| All existing/past seasons | Free | n/a | `RequiresPass = false` (grandfathered) |

SMS uplift (£5) must clear `cost-per-SMS × reminders-per-season` with margin (validate in Task 04).

## Access & Trial Rule (authoritative definition)

A user may take part in a season (join or create a league in it) if **any** of:

1. `Season.RequiresPass == false` (free season), **or**
2. A `SeasonPass` exists for `(UserId, SeasonId)`, **or**
3. The user has **never participated before** — no approved `LeagueMember` in *any* season **and** no `SeasonPass` of any kind — in which case **auto-grant a one-time Trial pass (Entry tier)** and allow.

Otherwise → **block, redirect to purchase page.**

"Participated" = holds/has held an approved `LeagueMember` row in any season **or** any `SeasonPass` record.

**Worked examples:**

| Scenario | Outcome |
|----------|---------|
| Brand-new user's first action is joining a PL 2026/27 league | Branch 3 → **free Trial pass** ✓ |
| User who played the free World Cup 2026, then tries PL | Has approved membership → branch 3 fails → **must purchase** ✓ |
| Existing user from a grandfathered past season tries PL | Has membership → **must purchase** ✓ |
| Trial user joins a 2nd PL league the same season | Branch 2 (pass exists) → allowed, **no second charge** ✓ |
| Trial user the following paid season | Has pass + membership history → **must purchase** ✓ |

Trial is **once per user, lifetime**, **Entry tier only** (no free SMS).

## SMS Reminder Behaviour & Early-Bird Reward

The SMS tier is designed to **reward getting predictions in early**, not to spam:

- **Emails run as now and stay free for everyone** at the early milestones (5 days, 3 days, 1 day).
- **SMS only fires in the final window** — at the **6-hour** milestone (and **1-hour** last-chance) — and **only if the user still hasn't submitted**. A user who predicts before the 6-hour mark receives **zero** SMS that round.
- At the 6h/1h milestones, an SMS-tier holder with a valid phone gets an **SMS instead of the email** (no double-message); everyone else continues to get the email.

We **track how many SMS each user is sent per season** (`SeasonPass.SmsSentCount`). This:

- gives cost visibility, and
- powers an **early-bird reward**: a user who was sent **fewer than a threshold (default 10) SMS** across a paid season **earns a free SMS upgrade for their next paid season** — so disciplined early submitters keep the peace-of-mind safety net for free.

> The reward needs a completed paid season of data first, so it begins from the **second** paid season onward (no reward on PL 2026/27, the first paid season). See [Task 13](./13-sms-earned-upgrade.md).

## Acceptance Criteria

- [ ] Sole trader registered; Monzo Business + Stripe live in the business name.
- [ ] `Season.RequiresPass` flag exists; all existing seasons + World Cup 2026 = `false`; PL 2026/27 = `true`.
- [ ] A user with no pass cannot join/create a league in a pass-required season unless trial-eligible.
- [ ] Brand-new users are auto-granted a free Entry trial on first participation in a pass-required season.
- [ ] Users can buy Entry or Entry + SMS via Stripe Checkout (one-off, Apple/Google Pay enabled).
- [ ] A `SeasonPass` is created reliably on successful payment (webhook-driven).
- [ ] SMS-tier holders receive deadline reminders by SMS **only in the final 6 hours and only if still unsubmitted**; emails run free at earlier milestones for everyone.
- [ ] Per-season SMS count tracked per user (`SmsSentCount`).
- [ ] Early-bird reward: users sent fewer than the threshold last paid season earn a free SMS upgrade next paid season.
- [ ] Terms & Privacy updated and flagged for solicitor review; refund/consumer-rights wording added.
- [ ] Domain project at 100% line + branch coverage; schema docs + DatabaseTools updated.

## Tasks

| # | Task | Description | Type |
|---|------|-------------|------|
| 1 | [Business setup (sole trader)](./01-business-setup-sole-trader.md) | Register with HMRC, records, day-job tax note | Offline |
| 2 | [Monzo Business account](./02-monzo-business-account.md) | Open free Lite account in business name | Offline |
| 3 | [Stripe account & products](./03-stripe-account-products.md) | Account, one-off Prices, Apple/Google Pay, webhook, keys | Offline |
| 4 | [Brevo SMS setup](./04-brevo-sms-setup.md) | Enable SMS, sender ID, credits, cost validation | Offline |
| 5 | [Legal page updates](./05-legal-page-updates.md) | Terms & Privacy edits, refund/consumer-rights clause | Code |
| 6 | [Domain model](./06-domain-season-pass.md) | `Season.RequiresPass`, `SeasonPass` entity + enums | Code |
| 7 | [Database & schema](./07-database-migration.md) | Migration, schema docs, DatabaseTools | Code |
| 8 | [Access gate & trial](./08-access-gate-and-trial.md) | Access rule + trial grant, wire into Join/Create | Code |
| 9 | [Stripe Checkout integration](./09-stripe-checkout-integration.md) | Checkout session command + webhook → `SeasonPass` | Code |
| 10 | [Purchase page](./10-purchase-page.md) | Blazor page from the mockup | Code |
| 11 | [SMS reminders](./11-sms-reminders.md) | Final-6h-only SMS for SMS-tier (email earlier), track per-season count | Code |
| 12 | [Testing & launch](./12-testing-and-launch.md) | Season config, Stripe test-mode E2E, go-live | Code/Manual |
| 13 | [SMS early-bird reward](./13-sms-earned-upgrade.md) | Free SMS upgrade next season for low-SMS users (2nd paid season on) | Code (follow-on) |

## Dependencies

- [x] Reminder system working (`SendScheduledRemindersCommandHandler`, `ReminderService`)
- [x] Brevo configured for email (`IEmailService`)
- [x] `Season` entity and league join flow (`JoinLeagueCommandHandler`)
- [x] Terms & Privacy pages (`/terms`, `/privacy`)
- [ ] **Need:** sole trader registration, Monzo Business, Stripe account, Brevo SMS enabled (Tasks 01–04)
- [ ] **Need:** solicitor review of legal pages before go-live

## Technical Notes

- Follow root `CLAUDE.md`: UK English, one public type per file, `DateTime.UtcNow` + `Utc` suffixes, CQRS (commands→repositories, queries→`IApplicationReadDbConnection`), bracketed PascalCase SQL with aliases, factory methods for new entities, **100% Domain coverage**, schema docs + DatabaseTools on any DB change.
- Stripe = one-off **`payment` mode** Checkout, never Billing/Subscriptions.
- Store only a Stripe **payment reference** on `SeasonPass` — never card data.

## Open Questions

- [ ] Final prices per competition (pending cost totals).
- [ ] Should trial-eligible users be allowed to *upgrade* their free Entry trial to SMS for the £5 difference? (Default: no, keep trial Entry-only for v1.)
- [ ] Add an in-app "pause SMS reminders" toggle now or defer? (Goodwill only; not required.)
- [ ] Early-bird reward threshold — confirm **<10 SMS/season** (configurable).
- [ ] Reward form — free SMS upgrade for the next season (default) vs a discount? How is it modelled on the pass (e.g. `Source = RewardUpgrade`, or SMS-comped flag with `AmountPaid` = Entry price)?
- [ ] Should SMS also fire at the 1-hour milestone, or 6-hour only? (Default: both 6h and 1h.)
