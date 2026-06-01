# Task: Brevo SMS Setup

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ⛔ Phase B — offline config; enables live SMS (not strictly blocked on the sole trader).

## Status

**Not Started** | In Progress | Complete

> **Deferred - SMS/Premium is out of the first-launch scope (June 2026).** Not started; revisit when the Premium (SMS) tier is added. (Brevo *email* is already configured and in use.)

## Type

**Offline** (Brevo dashboard) — enables the SMS channel Task 11 uses.

## Goal

Enable transactional SMS in Brevo, register a sender ID, buy credits, and confirm the per-message cost makes the £5 SMS tier worthwhile.

## Implementation Steps

### Step 1: Enable transactional SMS

- In Brevo → **Transactional → SMS** (or **SMS** in the left nav). Brevo SMS docs → https://help.brevo.com/hc/en-us/categories/360000179350-SMS
- Confirm the existing API key / account already used for email can also send SMS (same `IEmailService` infra account).

### Step 2: Register a sender ID

- Set an **alphanumeric sender ID**, e.g. `Predictns` (UK alphanumeric sender IDs: **max 11 characters**, letters/numbers).
- Note: alphanumeric senders are **one-way** (recipients can't reply) — fine, because we are **not** doing inbound STOP handling (these are transactional messages the user paid for).

### Step 3: Buy SMS credits & confirm the rate

- Top up SMS credits → check the **current UK per-SMS rate** in Brevo pricing → https://www.brevo.com/pricing/
- **Cost validation (do the maths before pricing the tier):**

| Item | Value |
|------|-------|
| Reminders per season (PL) | ~38 (1 per round) |
| Cost per SMS (UK) | _fill in from Brevo_ (e.g. ~4–5p) |
| Raw SMS cost per user/season | reminders × cost (e.g. ~£1.50–2.00) |
| SMS tier uplift | £5.00 |
| Margin per SMS user | uplift − raw cost (target comfortably positive) |

If a message exceeds 160 chars it counts as multiple SMS — keep reminder text short.

### Step 4: Keep content strictly transactional

- Reminder text = deadline only, e.g. `Predictns: PL GW12 deadline 11:00 Sat. Get your predictions in: <short link>`
- **No promotional content** — that keeps it a transactional/service message with **no legal opt-out requirement**.

## Verification

- [ ] SMS enabled; sender ID `Predictns` registered.
- [ ] Per-SMS UK rate recorded; tier margin confirmed positive.
- [ ] Test SMS sent successfully to your own phone.

## Notes

No inbound number / STOP keyword needed. The per-user in-app "pause SMS reminders" toggle (Task 11) covers opt-out without inbound SMS.

**UK-only:** SMS is offered only to **valid UK mobiles**, enforced **at purchase** and validated via `libphonenumber-csharp` (region `GB`, type `Mobile`), stored as **E.164** — see Task 10. The per-message cost assumptions here and the reward maths (ADR 0007) are UK-based; non-UK numbers are not offered SMS.
