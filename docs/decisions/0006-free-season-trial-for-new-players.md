# 0006. First season free (zero-pass trial; free play burns it)

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business

## Context

Whole-site gating (0005) removes the casual free on-ramp in paid seasons. We want a genuine newcomer to get **their first season free as a taster**, but **not** people who have already played — including those who only ever played free seasons.

## Decision

**Every season participation creates a `SeasonPass` record** (one per user/season — the per-user-per-season participation/entitlement record):

- Paid season → `Purchased` (or a comped `Trial`) record.
- **Free season → a £0 `Free` record.**

A user's **first season is free**, with eligibility defined as **zero `SeasonPass` records** (`COUNT == 0`). On the first pass-required season: if the user has 0 records, grant a free `Trial` pass (£0); otherwise they must purchase. The **Entry** portion of a trial is free; the SMS uplift is payable on top.

Because free seasons now create a record, **free-season play burns the freebie** — e.g. a World Cup 2026 player has a record, so their first Premier League season is **paid**. Existing free-season participation is **backfilled** with £0 `Free` records so existing players are treated the same way.

## Consequences

**For / positive**
- Dead-simple eligibility: a single `COUNT`/`EXISTS` on `SeasonPasses` — no `LeagueMember` history logic.
- Free play correctly **burns** the freebie (matches intent: the taster is your *first* season, whatever it is).
- Existing players are handled consistently via backfill.

**Against / cost**
- A one-time **backfill**: create a £0 `Free` record per existing approved `(user, season)` participation.
- Every participation writes a pass row (cheap; one per user/season).
- Existing grandfathered users have (backfilled) records → they **pay** for their first pass-required season. Intended.

**Neutral / notes**
- A user whose first-ever season is a free one (e.g. World Cup) "spends" the freebie there (it was free anyway) and pays for the next paid season.
- A refunded pass still counts as a record → no fresh free trial after a refund.

## Alternatives considered

- **Don't record free seasons (no burn)** — rejected; free play wouldn't consume the freebie, so World Cup players would also get the first Premier League season free, which is not wanted.
- **Eligibility by "never participated" (LeagueMember history)** — rejected; more complex than a `SeasonPass` `COUNT`.
- **Trial includes free SMS** — rejected; SMS has a real per-message cost, so the trial comps Entry only.

## Related

- 0002, 0005, 0007; `season-passes/06-domain-season-pass.md`, `07-database-migration.md`, `08-access-gate-and-trial.md`
