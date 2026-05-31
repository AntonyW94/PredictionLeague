# 0005. Season pass lifecycle: free seasons, free-first trial, refunds, no late entry

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business, financial

## Context

Introducing a paid model onto a previously-free site needs clear rules for: which seasons cost money, how a newcomer gets a taster, when money is refundable, and when entry closes.

## Decision

### a) Which seasons are paid
A season is pass-required when it has prices. `Season` exposes a **computed** `RequiresPayment => PassStandardPrice.HasValue` (no stored flag/column - refined during implementation; the original draft proposed a stored `RequiresPayment` boolean, but it was redundant with `PassStandardPrice` presence).
- All **existing/past seasons** stay free (grandfathered).
- **World Cup 2026** is **free for everyone**.
- **Premier League 2026/27 onward** require a pass.

World Cup is free because **its costs were already paid from the owner's personal account and will never be booked to the business** — there is nothing for the business to recover (not a business loss), and a free tournament builds the base. After it, **all seasons are paid**.

### b) First season free (zero-pass trial; free play burns it)
A user's **first season is free**, with eligibility = **zero `SeasonPass` records** (`COUNT == 0`). **Every participation writes a record** (one per user/season): paid → `Purchased`/`Trial`; **free season → a £0 `Free` record**. So **free play burns the freebie** (a World Cup player has a record → pays for PL). Existing free participation is **backfilled** with £0 `Free` records so existing players pay for their first paid season. The trial comps **Entry only**; the SMS uplift is payable on top.

### c) Refunds — before the season starts
A pass (incl. any SMS uplift) is **fully refundable until the season's first round deadline**; after that, non-refundable. Refunds go via Stripe and **revoke the entitlement**. A refunded pass **still counts as a record** (no fresh free trial). If a season is ever cancelled, affected paid users are refunded regardless.

### d) No late entry
**No late entry and no late/pro-rata pricing.** Paid seasons **inherit the existing per-league entry-deadline rules** (which already block late joins) — **no new access-gate mechanism**; the purchase page simply won't offer a pass once entry has closed.

## Consequences

**For / positive**
- Grandfathering is automatic (default-`false` flag), risk-free for existing data.
- Trial eligibility is a single cheap `COUNT`/`EXISTS` on `SeasonPasses` — no `LeagueMember` history logic — and free play correctly burns the freebie.
- Refunds give goodwill (change-of-mind, cancellation) with a simple cut-off shared with entry.
- No late-entry logic to build.

**Against / cost**
- One-time **backfill** of £0 `Free` records for existing approved memberships.
- Every participation writes a row (cheap, one per user/season).
- Existing players (backfilled records) **pay** for their first paid season — intended.

**Neutral / notes**
- `SeasonPassSource` = `Purchased | Trial | Free` (+ `RewardUpgrade` open question, 0007). Refund marker: `RefundedAtUtc`.
- A refunded pass keeps its record, preventing buy→refund→free-again gaming.

## Alternatives considered

- **Charge for the World Cup** — rejected; costs are personal/sunk, nothing to recoup; free tournament builds the base.
- **Don't record free seasons (no burn)** — rejected; free play wouldn't consume the freebie, letting World Cup players also get PL free.
- **Eligibility by "never participated" (LeagueMember history)** — rejected; more complex than a `SeasonPass` `COUNT`.
- **Strictly non-refundable** — rejected; poor goodwill for a friends-and-family launch.
- **Late-join / pro-rata pricing** and **a new "entry closed" exception** — rejected; unwanted behaviour, and redundant with existing entry deadlines.

## Related

- 0002, 0006 (pricing), 0007 (SMS reward uses pass length/competition); `season-passes/06-domain-season-pass.md`, `07-database-migration.md`, `08-access-gate-and-trial.md`, `10-purchase-page.md`, `17-refunds.md`, `05-legal-page-updates.md`.
