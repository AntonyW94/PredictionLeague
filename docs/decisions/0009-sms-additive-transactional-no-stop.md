# 0009. SMS additive, final-window, transactional-only, no inbound STOP

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, legal

## Context

How and when to send the paid SMS, and what compliance is needed. UK rules (PECR) require an opt-out (STOP) for **marketing** SMS but **not** for purely **transactional/service** messages. Inbound STOP handling needs two-way SMS (extra cost/complexity).

## Decision

- **SMS is additive:** everyone keeps **all** email reminders at every milestone; SMS-tier holders get an **extra** text **only at the final window (6h and 1h)** and **only if they still haven't submitted**.
- SMS content is **strictly transactional** (deadline only, no promo).
- **No inbound STOP** keyword handling; instead we **build an in-app "pause SMS" toggle** so SMS-tier holders can switch their texts off for the season.
- The SMS tier follows the season-pass **refund policy (0019)**: refundable as part of the pass **before the season starts**, non-refundable after; pausing/disabling reminders is **not** a refund.
- The SMS tier requires a **valid UK mobile**, enforced **at purchase** (validated via `libphonenumber-csharp`, stored as E.164) — see 0009 notes and Task 10.

## Consequences

**For / positive**
- SMS subscribers lose nothing (emails still arrive) — SMS is pure upside.
- Transactional-only content means **no legal opt-out requirement**, so no two-way SMS plumbing/cost.
- Early submitters receive zero texts → low cost and it rewards the behaviour we want (feeds 0010).
- No refund exposure from opt-outs.

**Against / cost**
- Must keep message content disciplined (any promo line would flip it to marketing and require opt-out).

**Neutral / notes**
- Final-window milestones = **2** (6h + 1h); configurable.
- The in-app pause toggle is **in scope for v1** (built now), not deferred.
- **UK-only:** SMS is offered only to valid UK mobiles; the per-message cost assumptions (and the reward maths in 0010) are UK-based. Non-UK numbers are not offered SMS.

## Alternatives considered

- **SMS replaces the email at 6h/1h** — rejected; subscribers shouldn't lose the email they'd otherwise get.
- **SMS at all milestones** — rejected; costly and spammy.
- **Inbound STOP** — rejected; not required for transactional SMS and adds cost/complexity.

## Related

- 0008, 0010; `season-passes/04-brevo-sms-setup.md`, `11-sms-reminders.md`
