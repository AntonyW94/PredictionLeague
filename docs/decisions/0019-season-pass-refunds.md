# 0019. Season pass refunds (refundable before the season starts)

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, legal, financial

## Context

Earlier drafts treated passes as effectively non-refundable (immediate-access waiver of the 14-day right) and the SMS tier as non-refundable. In practice we want a goodwill refund path: let someone who changes their mind get a refund **before the season begins**, and have a process to refund in the (considered very unlikely) event a season is cancelled. These earlier statements were uncommitted drafts in this branch, so they're edited in place rather than superseded.

## Decision

A season pass (including any SMS uplift) is **fully refundable until the season starts** — defined as its **first round's deadline**. After the season has started it is **non-refundable** (consistent with the immediate-use waiver in the Terms). Refunds are issued via **Stripe**; on refund the `SeasonPass` entitlement is **revoked**. If a season is ever cancelled, affected paid users are refunded regardless of start.

## Consequences

**For / positive**
- Customer goodwill; covers change-of-mind and the unlikely season-cancellation case.
- Simple, defensible cut-off: the season start.

**Against / cost**
- Build a refund flow (Stripe refund + entitlement revocation + a pass status).
- Reward interaction: a refunded SMS pass must **not** count toward earning a free upgrade (its `SmsFeePaid` is effectively reversed) — see 0010.

**Neutral / notes**
- Pre-start refunds should be rare; this is mostly a safety valve.
- Revises the "non-refundable" wording in 0009 and the Terms clause in Task 05 (edited in place).

## Alternatives considered

- **Strictly non-refundable** — rejected; poor goodwill for friends-and-family launch.
- **Full 14-day cooling-off even after the season starts** — rejected; we waive that on immediate use, and the pre-start window covers genuine change-of-mind.

## Related

- 0009, 0010; `season-passes/05-legal-page-updates.md`, `17-refunds.md`
