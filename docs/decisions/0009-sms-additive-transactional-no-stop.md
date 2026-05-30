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
- **No inbound STOP** keyword handling; an optional **in-app "pause SMS" toggle** may be offered as goodwill.
- The SMS tier is **non-refundable**; disabling reminders does not trigger a refund.

## Consequences

**For / positive**
- SMS subscribers lose nothing (emails still arrive) — SMS is pure upside.
- Transactional-only content means **no legal opt-out requirement**, so no two-way SMS plumbing/cost.
- Early submitters receive zero texts → low cost and it rewards the behaviour we want (feeds 0010).
- No refund exposure from opt-outs.

**Against / cost**
- Must keep message content disciplined (any promo line would flip it to marketing and require opt-out).

**Neutral / notes**
- Number of final-window milestones (default 2: 6h + 1h) is configurable.

## Alternatives considered

- **SMS replaces the email at 6h/1h** — rejected; subscribers shouldn't lose the email they'd otherwise get.
- **SMS at all milestones** — rejected; costly and spammy.
- **Inbound STOP** — rejected; not required for transactional SMS and adds cost/complexity.

## Related

- 0008, 0010; `season-passes/04-brevo-sms-setup.md`, `11-sms-reminders.md`
