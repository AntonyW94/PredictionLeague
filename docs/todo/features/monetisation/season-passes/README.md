# Feature: Season Passes (Website Monetisation)

## Status

**Not Started** | In Progress | Complete

## Summary

Introduce a paid, one-off **Season Pass** that gates participation in pass-required seasons (e.g. Premier League 2026/27). A pass is a **one-off charge for access to the software/service** — explicitly **not** a stake, bet, or entry into any prize fund — sold in two tiers: **Standard** and **Premium** (Premium adds SMS deadline reminders). Free seasons (e.g. World Cup 2026) and all existing seasons stay free. **First-time players get their first season free** as a trial. The Predictions never holds prize money; paid-league entry fees remain a private, peer-to-peer matter between members.

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
│  │  Season Standard       │   │  Season Premium  ★   │    │
│  │  £10  one-off       │   │  £15  one-off            │    │
│  │  ✓ Join all leagues │   │  ✓ Everything in Standard   │    │
│  │  ✓ Predictions      │   │  ✦ SMS deadline reminders│    │
│  │  ✓ Leaderboards     │   │                          │    │
│  │  ✓ Email reminders  │   │                          │    │
│  │  [ Get Standard ]      │   │  [ Get Premium ]     │    │
│  └─────────────────────┘   └─────────────────────────┘    │
│   🔒 Secure payment via Stripe · one-off, no auto-renewal  │
└──────────────────────────────────────────────────────────┘
```

Trial-eligible (brand-new) users see a "Your first season is on us" state instead of prices.

## Pricing Model (admin-configurable, calculator-suggested)

Prices are **not hardcoded**. Each pass-required season stores an admin-set **Standard price** and **Premium price** (DB-backed, editable in admin, sent to Stripe as **dynamic amounts** so no fixed Stripe Prices are created by hand). During season creation the admin sees a **recommended price** from the running-costs calculator (Tasks 14–15), which they can override.

**Calculator rules (confirmed with owner):**

- **Goal:** recommended price = covered costs **+ ~15% buffer**.
- **Cost apportionment:** annual running costs are split across the year's paid seasons **weighted by season length** (rounds/duration) — a long Premier League season carries more than a short cup.
- **Denominator = expected players:** the **distinct participant count of the last completed season of the same competition**, i.e. recommend a price that **breaks even at roughly last season's player numbers**.
- **Business-borne costs only:** costs still paid from the owner's **personal** account are **excluded until their renewal date**, when they move to the business and enter the calculation. (Owner migrates each cost to the business bank as it renews.)
- **Free seasons:** World Cup 2026 is free (no prices set), run at a **deliberate one-off loss**. **After it, all seasons are paid**, so no ongoing free-season subsidy logic is needed.
- Standard recommendation is grossed up for **Stripe fees**; the **SMS uplift** reflects expected SMS cost per SMS-user over the season (Task 04 rate) + buffer.
- **Always an editable, pre-filled info box** on the create-season page — never enforced. If there's **no comparable prior season** to derive player numbers, leave it **blank with explanatory wording**. Apply a **small minimum floor** (covers Stripe fees + a little), still editable below.
- Running costs are stored with **cost type, price, and start/end dates** so apportionment/proration can be done any way in future (Task 14).

| Season | Pass required? | Price |
|--------|----------------|-------|
| All existing/past seasons | No (grandfathered) | Free |
| World Cup 2026 | No (deliberate loss) | Free |
| Premier League 2026/27 onward | Yes | Admin-set, calculator-suggested |

## Access & Trial Rule (authoritative definition)

**Acquire-first.** A Season Pass is required to take part in **every** season; the user **acquires** a pass (free or paid) **before** they can see/join that season's public leagues. Two distinct steps:

**The gate** (on join/create, and on viewing a season's public leagues):
- A `SeasonPass` exists for `(UserId, SeasonId)` → **allow**.
- Otherwise → **block** (`SeasonPassRequiredException` → 402) and **redirect to the acquire page** for that season.

**Acquisition** (the explicit "Get your pass" action — `AcquireSeasonPassCommand`):
1. Pass already exists → **idempotent** (nothing to do), **or**
2. **Free season** (`!Season.RequiresPayment`, i.e. `PassStandardPrice IS NULL`) → grant a **£0 `Free` pass** (records participation — burns the freebie), **or**
3. **Paid season + zero `SeasonPass` records** (`COUNT == 0`) → grant a **free `Trial` pass** (first season free), **or**
4. **Paid season + ≥1 record** → **payment required → Stripe checkout** (Phase B).

Eligibility for the trial is a single `COUNT`/`EXISTS` on `SeasonPasses` — **no `LeagueMember` history check**. **Every acquisition writes a record** (free → £0 `Free`), so **free play burns the freebie**. Late entry is handled by the existing per-league entry-deadline rules (ADR 0005).

**Worked examples:**

| Scenario | Outcome |
|----------|---------|
| Brand-new user's first-ever season is PL 2026/27 | 0 records → **free Trial pass** ✓ |
| User who played the free World Cup 2026, then tries PL | Has a £0 `Free` record from WC → **must purchase** ✓ |
| Existing grandfathered user tries PL | Backfilled `Free` record(s) → **must purchase** ✓ |
| Trial user joins a 2nd PL league the same season | Branch 1 (pass exists) → allowed, **no second charge** ✓ |
| User who already has any pass tries a later paid season | ≥1 record → **must purchase** ✓ |
| User who bought then refunded, tries a later season | Refunded record still counts → **must purchase** (no re-trial) ✓ |

Trial is **once per user, lifetime**. The **Standard portion is free**; a trial user who wants SMS may **pay just the SMS uplift** on top (the trial only comps Standard).

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
- [ ] Pass-required is derived from price (`Season.PassStandardPrice IS NOT NULL`); all existing seasons + World Cup 2026 are free (no prices); PL 2026/27 is priced.
- [ ] A user with no pass cannot join/create a league in a pass-required season unless trial-eligible.
- [ ] Brand-new users are auto-granted a free Standard trial on first participation in a pass-required season.
- [ ] Per-season **Standard** and **Premium** prices are admin-configurable (DB-backed); Stripe charges those exact amounts (dynamic).
- [ ] Admin **Running Costs** page records costs, renewal dates, and payer status (business vs personal-until-renewal).
- [ ] Season creation shows a **recommended price** from the calculator (15% buffer, length-weighted apportionment, break-even at last comparable season's player count, business-borne costs only).
- [ ] Users can buy Standard or Premium via Stripe Checkout (one-off, Apple/Google Pay enabled).
- [ ] A `SeasonPass` is created reliably on successful payment (webhook-driven).
- [ ] Everyone (incl. SMS-tier) keeps all emails at every milestone; SMS is an **additional** 6h/1h nudge for unsubmitted SMS-tier holders.
- [ ] Per-season SMS count tracked per user (`SmsSentCount`).
- [ ] Self-funding reward: a paying low-usage SMS season earns the next SMS season free when the leftover fee covers that season's worst-case SMS cost.
- [ ] Passes (incl. SMS) are refundable before the season starts (Stripe refund + entitlement revoked); non-refundable after.
- [ ] Email verification completed: unconfirmed users can't purchase/take part; `+`-alias emails are rejected as duplicates.
- [ ] SMS purchase requires a valid UK mobile (libphonenumber, E.164); blocked until one is added.
- [ ] Terms & Privacy updated and flagged for solicitor review; refund/consumer-rights wording added.
- [ ] Domain project at 100% line + branch coverage; schema docs + DatabaseTools updated.

## Build Phases & Readiness

> **How to use this plan:** this branch contains the **plan only** (no feature code yet). Execution happens in a separate session — work **Phase A top-down** (none of it needs any account), then **Phase B** once the business accounts exist. Each task file carries its own **Readiness** line.

Work is split by what needs a **business entity / live accounts** (sole trader → Monzo Business → Stripe → Brevo SMS) and what doesn't.

- **Phase A — buildable now (no accounts needed):** pure domain/DB/UI/logic work. None of it requires the sole trader, a bank, or Stripe/Brevo keys. **Start here, top-down.**
- **Phase B — needs business setup:** anything that touches **live Stripe** (real charges/refunds) or **live SMS sending** (Brevo credits), plus the offline account setup itself and final go-live.

Task **ID numbers are stable** (they're referenced across the ADRs and other tasks); the lists below are the **recommended build order**, not the file numbers. A couple of tasks are **partial** — the bulk is Phase A, with an account-dependent slice deferred to Phase B (noted inline).

### Phase A — do now (no business accounts required)

| Order | # | Task | Notes |
|-------|---|------|-------|
| A1 | 16 | [Competitions management](./16-competitions-management.md) | Foundational — `Competitions` table + `Season.CompetitionId`; refactors existing sync. Do first. |
| A2 | 6 | [Domain model](./06-domain-season-pass.md) | `Season` changes, `SeasonPass`, enums. |
| A3 | 7 | [Database & schema](./07-database-migration.md) | All new tables/columns, backfills, schema doc, DatabaseTools. |
| A4 | 8 | [Access gate & trial](./08-access-gate-and-trial.md) | **Acquire-first (backend):** gate = holds-a-pass check on Join/Create (else 402); `AcquireSeasonPassCommand` grants free (free season) / trial (first paid season). |
| A4b | 8 | Acquire UI + passes pages + visibility gating | **Follow-up to A4:** acquire endpoint + "Get your pass" page (handles the 402 redirect; free = 1-click £0), **My Passes** + **Available Passes** pages, and per-season **public-league visibility gating**. Free path is Phase A; the paid acquire is Stripe (Phase B). **Deploy gate: A4's block must not go live until this UI exists.** |
| A5 | 19 | [Entry-fee settlement](./19-entry-fee-settlement.md) | Encryption service + admin bank details + join/pay flow (peer-to-peer, no Stripe). |
| A6 | 20 | [Payouts](./20-payouts.md) | Player payout details + payouts list + mark-as-paid (manual, no Stripe). |
| A7 | 14 | [Admin running costs](./14-admin-running-costs.md) | Running-costs CRUD page. |
| A8 | 15 | [Configurable prices & calculator](./15-configurable-prices-and-calculator.md) | Per-season prices + recommended-price calculator (maths only; uses fee constants). |
| A9 | 18 | [Email verification & identity](./18-email-verification-and-identity.md) | Finish confirmation + `+`-alias normalisation (existing Brevo email). |
| A10 | 13 | [SMS early-bird reward](./13-sms-earned-upgrade.md) | Reward eligibility logic (no live SMS needed to build). |
| A11 | 11 | [SMS reminders](./11-sms-reminders.md) | **Partial:** build the job split + `ISmsService`/`BrevoSmsService` + unit tests now; **live sending** needs Brevo SMS (→ Phase B). |
| A12 | 10 | [Purchase page](./10-purchase-page.md) | **Partial:** build the page + trial/reward/closed states now; the **Stripe Checkout redirect** needs Stripe (→ Phase B). |
| A13 | 5 | [Legal page updates](./05-legal-page-updates.md) | Draft the Terms/Privacy edits now; **solicitor review** is a go-live gate (Phase B). |

### Phase B — needs business setup / live accounts

| Order | # | Task | Blocked on |
|-------|---|------|------------|
| B1 | 1 | [Business setup (sole trader)](./01-business-setup-sole-trader.md) | 🟡 **In progress** — registered with HMRC; **UTR in the post (up to 28 days)**. |
| B2 | 2 | [Monzo Business account](./02-monzo-business-account.md) | ✅ **Done** — account open. |
| B3 | 3 | [Stripe account & products](./03-stripe-account-products.md) | 🟢 **Unblocked** — Monzo open; **UTR not required to register**. Can start now. |
| B4 | 4 | [Brevo SMS setup](./04-brevo-sms-setup.md) | Offline config (not blocked on the sole trader — can be done anytime, but enables Phase-B SMS). |
| B5 | 9 | [Stripe Checkout integration](./09-stripe-checkout-integration.md) | Stripe keys/account. |
| B6 | 17 | [Refunds](./17-refunds.md) | Stripe (refund API). |
| B7 | 10 | Purchase page — finish | Wire the Stripe Checkout redirect (rest built in A12). |
| B8 | 11 | SMS reminders — go live | Enable live sending + Brevo credits (code built in A11). |
| B9 | 12 | [Testing & launch](./12-testing-and-launch.md) | Live Stripe + Brevo; solicitor sign-off on Terms. |

## Dependencies

- [x] Reminder system working (`SendScheduledRemindersCommandHandler`, `ReminderService`)
- [x] Brevo configured for email (`IEmailService`)
- [x] `Season` entity and league join flow (`JoinLeagueCommandHandler`)
- [x] Terms & Privacy pages (`/terms`, `/privacy`)
- [x] **Sole trader** registered with HMRC (UTR in the post, up to 28 days) (Task 01)
- [x] **Monzo Business** account open (Task 02)
- [ ] **Need:** Stripe account (unblocked now - UTR not required to register), Brevo SMS enabled (Tasks 03-04)
- [ ] **Need:** solicitor review of legal pages before go-live

## Technical Notes

- Follow root `CLAUDE.md`: UK English, one public type per file, `DateTime.UtcNow` + `Utc` suffixes, CQRS (commands→repositories, queries→`IApplicationReadDbConnection`), bracketed PascalCase SQL with aliases, factory methods for new entities, **100% Domain coverage**, schema docs + DatabaseTools on any DB change.
- Stripe = one-off **`payment` mode** Checkout, never Billing/Subscriptions.
- Store only a Stripe **payment reference** on `SeasonPass` — never card data.

## Related / Future Features

- **[Season Challenges (badges)](../season-challenges/)** — earnable, show-off badges tied to a season pass (e.g. "Early Bird", "Perfect Round"). Separate follow-up; relates to `user-experience/achievements-badges`.

## Resolved Decisions

These were decided this session (see `docs/decisions/`):

- **Reward eligibility** → **same competition only**, worst case assumes the next same-competition season ≈ the current one's length (ADR 0007).
- **Final-window milestones** → **2** (6h + 1h) for both reminders and reward maths (ADR 0007).
- **Recommended price** → always an **editable, pre-filled info box**; **blank + explanatory wording** when no comparable prior season; **small minimum floor** (ADR 0006).
- **Running-cost data** → store **cost type, price, start/end dates** for flexible future apportionment (ADR 0006, Task 14).
- **Free trial = zero `SeasonPass` records** → a user's first season is free (a `Trial` pass on the first *paid* season). **Every participation writes a record** — free seasons get a £0 `Free` record — so **free play burns the freebie**; existing free play is **backfilled** so existing players pay for their first paid season (ADR 0005). Trial comps **Standard only**; user may **pay the SMS uplift** on top.
- **Pause-SMS toggle** → **build now** (in scope), not deferred (ADR 0007).
- **Refunds** → passes (incl. SMS) refundable **before the season starts**, non-refundable after; covers cancellation (ADR 0005, Task 17).
- **Email verification** → finish it and **normalise emails (strip `+` alias)** to stop multi-account trial abuse (ADR 0009, Task 18).
- **SMS = UK mobiles only**, required and validated (libphonenumber → E.164) **at purchase** (ADR 0007, Task 10).
- **No late entry** → handled by the **existing per-league entry-deadline rules** (paid seasons inherit them); no new access-gate mechanism, just don't offer purchase once entry has closed. No late/pro-rata pricing (ADR 0005, Task 10).
- **Overlapping seasons** → already supported: `SeasonPasses` holds **one row per (user, season)** with a unique index, so a user can hold concurrent passes (e.g. World Cup + Premier League). **No new table needed.** Free/grandfathered seasons **do** get a £0 `Free` record (and existing ones are backfilled) so free play burns the free-first-season — this is the chosen approach over a no-record/`RequiresPayment`-only gate.
- **Entry-fee settlement (peer-to-peer)** → admins can store **bank details (encrypted at rest)** on a league; the entry code is shared freely, players see the details + amount + reference on requesting to join, and the **admin accepts once paid**. The software never touches the money (ADR 0010, 0003; Task 19).
- **Payouts (peer-to-peer)** → players may **optionally** store **encrypted payout details** (visible only to the named admin(s) of leagues they're in; deletable; manual fallback if not provided; a **join-time warning** names the admin who'll gain visibility, with a remove button). The admin gets a **payouts list** showing **one aggregated total per winner** (tracked in `LeaguePayouts`) with the Round/Monthly/Overall **breakdown computed live from `Winnings`** (source of truth — not duplicated). **Mark-as-paid is only available once the season is complete** (all rounds done), then it's a clean seam to automate later only with legal sign-off (ADR 0010, 0008; Task 20).
- **Comparable season / "same competition"** = matched on `Season.CompetitionId`, a FK to a new **`Competitions` reference table** (ADR 0009), **not** `ApiLeagueId`. The table carries a **hosted logo**, a **`Type`** (League/Tournament, moved off `Season`), and an **admin-editable API league id**; `Season` **drops `ApiLeagueId` and `CompetitionType`** and the sync/type resolve from the competition (Task 16). Switching fixture provider is a no-deploy admin edit that never invalidates free-SMS entitlements or price comparables.

## Open Questions

- [ ] **Reward display detail** — show remaining SMS budget as £ or as a message count, and where it appears (dashboard / pass page).
- [ ] Whether to add a distinct `SeasonPassSource.RewardUpgrade` value, or keep the `SmsFeePaid = 0` + `RewardRedeemedForSeasonId` modelling (Task 13's default).
