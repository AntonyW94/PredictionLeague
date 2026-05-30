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

## Pricing Model (admin-configurable, calculator-suggested)

Prices are **not hardcoded**. Each pass-required season stores an admin-set **Entry price** and **Entry + SMS price** (DB-backed, editable in admin, sent to Stripe as **dynamic amounts** so no fixed Stripe Prices are created by hand). During season creation the admin sees a **recommended price** from the running-costs calculator (Tasks 14–15), which they can override.

**Calculator rules (confirmed with owner):**

- **Goal:** recommended price = covered costs **+ ~15% buffer**.
- **Cost apportionment:** annual running costs are split across the year's paid seasons **weighted by season length** (rounds/duration) — a long Premier League season carries more than a short cup.
- **Denominator = expected players:** the **distinct participant count of the last completed season of the same competition**, i.e. recommend a price that **breaks even at roughly last season's player numbers**.
- **Business-borne costs only:** costs still paid from the owner's **personal** account are **excluded until their renewal date**, when they move to the business and enter the calculation. (Owner migrates each cost to the business bank as it renews.)
- **Free seasons:** World Cup 2026 is `RequiresPass = false`, run at a **deliberate one-off loss**. **After it, all seasons are paid**, so no ongoing free-season subsidy logic is needed.
- Entry recommendation is grossed up for **Stripe fees**; the **SMS uplift** reflects expected SMS cost per SMS-user over the season (Task 04 rate) + buffer.
- **Always an editable, pre-filled info box** on the create-season page — never enforced. If there's **no comparable prior season** to derive player numbers, leave it **blank with explanatory wording**. Apply a **small minimum floor** (covers Stripe fees + a little), still editable below.
- Running costs are stored with **cost type, price, and start/end dates** so apportionment/proration can be done any way in future (Task 14).

| Season | Pass required? | Price |
|--------|----------------|-------|
| All existing/past seasons | No (grandfathered) | Free |
| World Cup 2026 | No (deliberate loss) | Free |
| Premier League 2026/27 onward | Yes | Admin-set, calculator-suggested |

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

Trial is **once per user, lifetime**. The **Entry portion is free**; a trial user who wants SMS may **pay just the SMS uplift** on top (the trial only comps Entry).

## SMS Reminder Behaviour & Early-Bird Reward

SMS is an **extra** safety net layered on top of the existing free emails — SMS-tier holders lose nothing:

- **Everyone keeps every email at every milestone** (5d, 3d, 1d, **6h, 1h**), exactly as now — including SMS subscribers.
- **SMS is additional:** at the **6h** and **1h** milestones, an SMS-tier holder with a valid phone *also* gets a text **only if they still haven't submitted**. Submit before 6h and you get **zero** texts that round (you still got the emails).
- So SMS never replaces an email; it's a bonus nudge in the final window for people who haven't acted.

We **track how many SMS each user is sent per season** (`SeasonPass.SmsSentCount`). This:

- gives cost visibility, and
- powers a **self-funding early-bird reward** (below).

**Reward rule (profit-based — the "10" is computed, not hardcoded):** the SMS fee a user pays must first cover the texts actually sent to them (`SmsSentCount × price-per-message`). If enough of that fee is **left over to cover the worst case of the season they buy next** — `rounds × final-window-milestones × price-per-message` (e.g. PL 38 × 2 = **76** possible texts, World Cup 7 × 2 = **14**) — then their **next SMS season is free**. The qualifying allowance **varies with season length and the live per-message rate**, and is only known once actual costs are entered.

- **Same competition only**, and because the next season may not exist yet, the worst case **assumes the next same-competition season is the same length as the current one** (uses the earning season's `rounds × 2`).
- Evaluated against the **current per-message rate**; a free (comped) season pays no fee, so it **cannot itself fund another free season** — a *paying* low-usage season earns the *next same-competition* one free.
- In-app, SMS-tier holders see their **remaining SMS budget** (`fee − texts used × rate`) — a budget to protect by getting predictions in early, not a bar to fill.

> Begins from the **second** paid season of a competition onward (no reward on PL 2026/27). Storage and the same-competition rule are detailed in [Task 13](./13-sms-earned-upgrade.md).

## Acceptance Criteria

- [ ] Sole trader registered; Monzo Business + Stripe live in the business name.
- [ ] `Season.RequiresPass` flag exists; all existing seasons + World Cup 2026 = `false`; PL 2026/27 = `true`.
- [ ] A user with no pass cannot join/create a league in a pass-required season unless trial-eligible.
- [ ] Brand-new users are auto-granted a free Entry trial on first participation in a pass-required season.
- [ ] Per-season **Entry** and **Entry + SMS** prices are admin-configurable (DB-backed); Stripe charges those exact amounts (dynamic).
- [ ] Admin **Running Costs** page records costs, renewal dates, and payer status (business vs personal-until-renewal).
- [ ] Season creation shows a **recommended price** from the calculator (15% buffer, length-weighted apportionment, break-even at last comparable season's player count, business-borne costs only).
- [ ] Users can buy Entry or Entry + SMS via Stripe Checkout (one-off, Apple/Google Pay enabled).
- [ ] A `SeasonPass` is created reliably on successful payment (webhook-driven).
- [ ] Everyone (incl. SMS-tier) keeps all emails at every milestone; SMS is an **additional** 6h/1h nudge for unsubmitted SMS-tier holders.
- [ ] Per-season SMS count tracked per user (`SmsSentCount`).
- [ ] Self-funding reward: a paying low-usage SMS season earns the next SMS season free when the leftover fee covers that season's worst-case SMS cost.
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
| 13 | [SMS early-bird reward](./13-sms-earned-upgrade.md) | Self-funding free SMS upgrade next season (2nd paid season on) | Code (follow-on) |
| 14 | [Admin running costs](./14-admin-running-costs.md) | Page to record annual costs, renewal dates, payer status | Code |
| 15 | [Configurable prices & calculator](./15-configurable-prices-and-calculator.md) | Per-season admin prices + recommended-price calculator | Code |

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

## Related / Future Features

- **[Season Challenges (badges)](../season-challenges/)** — earnable, show-off badges tied to a season pass (e.g. "Early Bird", "Perfect Round"). Separate follow-up; relates to `user-experience/achievements-badges`.

## Resolved Decisions

These were decided this session (see `docs/decisions/`):

- **Reward eligibility** → **same competition only**, worst case assumes the next same-competition season ≈ the current one's length (ADR 0010).
- **Final-window milestones** → **2** (6h + 1h) for both reminders and reward maths (ADR 0009).
- **Recommended price** → always an **editable, pre-filled info box**; **blank + explanatory wording** when no comparable prior season; **small minimum floor** (ADR 0012).
- **Running-cost data** → store **cost type, price, start/end dates** for flexible future apportionment (ADR 0012, Task 14).
- **Trial + SMS** → trial comps **Entry only**; user may **pay the SMS uplift** on top (ADR 0006).
- **Pause-SMS toggle** → **build now** (in scope), not deferred (ADR 0009).
- **Comparable season / "same competition"** = matched on a new internal **`Competition` enum** on `Season` (ADR 0017), **not** `ApiLeagueId` — so switching fixture provider never invalidates free-SMS entitlements or price comparables. `ApiLeagueId` stays as the provider sync mapping only.

## Open Questions

- [ ] **Reward display detail** — show remaining SMS budget as £ or as a message count, and where it appears (dashboard / pass page).
- [ ] Whether to add a distinct `SeasonPassSource.RewardUpgrade` value, or keep the `SmsFeePaid = 0` + `RewardRedeemedForSeasonId` modelling (Task 13's default).
