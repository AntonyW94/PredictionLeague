# Task: SMS Reminders

**Parent Feature:** [README.md](./README.md)

> **Readiness:** 🟡 Phase A (partial) — build the job split + `ISmsService`/`BrevoSmsService` + tests now; **live SMS sending needs Brevo SMS (Phase B)**.

## Status

**Not Started** | In Progress | Complete

## Goal

Layer SMS **on top of** the existing emails: **everyone keeps every email at every milestone**, and SMS-tier holders *additionally* get a text at the 6h and 1h milestones **only if they still haven't submitted**. Track how many SMS each user is sent per season.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs` | Modify | Send the usual email to everyone, plus an additional SMS to eligible SMS-tier holders at 6h/1h |
| `src/ThePredictions.Application/Services/IReminderService.cs` | Modify | Expose SMS-eligible recipients |
| `src/ThePredictions.Infrastructure/Services/ReminderService.cs` | Modify | Query SMS-tier holders for the season |
| `src/ThePredictions.Application/Services/ISmsService.cs` | Create | SMS send abstraction |
| `src/ThePredictions.Infrastructure/Services/Sms/BrevoSmsService.cs` | Create | Brevo SMS implementation |

## Reminder Channel Matrix

Email is unchanged for **everyone**. SMS is **additional** (never a replacement):

| Milestone | Email (everyone) | Extra SMS (SMS-tier, still unsubmitted) |
|-----------|------------------|------------------------------------------|
| 5 days | ✅ Email | — |
| 3 days | ✅ Email | — |
| 1 day | ✅ Email | — |
| **6 hours** | ✅ Email | ➕ **also** SMS |
| **1 hour** | ✅ Email | ➕ **also** SMS |

SMS therefore **only ever fires in the final window (6h/1h)**, **only if predictions are still missing**, and **on top of** the email. An early submitter gets **zero** SMS that round (but still got every email). (Number of final-window milestones is configurable — default 2; see README open question.)

## Implementation Steps

### Step 1: Email is unchanged

- The existing email reminder flow runs **exactly as now** for all users at all milestones, including SMS-tier holders. Do not suppress any email.

### Step 2: Add the extra SMS (final window only)

- After the email step, at the **6h and 1h** milestones, additionally send an **SMS** to a user when **all** hold:
  1. they hold a `SeasonPass` with `Tier == Premium` for the round's season,
  2. they have **not paused** SMS for the season (`SeasonPass.SmsPaused == false`),
  3. they have a valid mobile number, and
  4. they still haven't submitted predictions for the round.
- Identify eligible recipients via `IApplicationReadDbConnection` (query side), joining `SeasonPasses` to the users missing predictions for the upcoming round.

```
# existing email path runs first, unchanged, for everyone

if milestone is in final window (6h / 1h):
    for each SMS-tier user, not paused, still missing predictions, with a valid phone:
        send SMS via ISmsService          (short, transactional text + link)
        pass.RecordSmsSent()              (increment SeasonPass.SmsSentCount, persist via repository)
```

### Step 4: In-app "pause SMS" toggle (in scope for v1)

- Add a per-season toggle on the user's notification/account settings: **"Pause SMS reminders for this season"**, setting `SeasonPass.SmsPaused`.
- Paused users still receive **all emails**; no refund (the SMS tier is non-refundable — ADR 0007). They can un-pause at any time.
- This replaces inbound STOP handling (we don't do two-way SMS).

- Track SMS dispatch separately so the **same SMS isn't sent twice** for the same milestone (e.g. an `SmsReminderSentUtc`/per-milestone marker), independent of the existing email `LastReminderSentUtc`.
- SMS body ≤160 chars; transactional only (no promo) — see Task 04.
- Incrementing `SmsSentCount` powers the early-bird reward (Task 13). The reminder job is a **command** path, so update the count via the `SeasonPass` repository, not the read connection.

### Step 3: Brevo SMS implementation

- `BrevoSmsService.SendAsync(toNumber, message)` using the Brevo SMS API + registered sender ID. Config (API key, sender) from Key Vault/settings as per email.

## Code Patterns to Follow

Mirror the existing email reminder flow and `IEmailService` wiring. UK date formatting via existing `UkEmailDateFormatter` where shown in the link.

## Verification

- [ ] All users (incl. SMS-tier) still get the existing email at **every** milestone — no email suppressed.
- [ ] At 6h/1h, an unsubmitted SMS-tier holder with a valid phone gets an SMS **in addition** to the email.
- [ ] A user who submitted before the 6h mark receives **no** SMS that round (still got the emails).
- [ ] `SmsSentCount` increments by exactly one per SMS actually sent.
- [ ] No duplicate SMS for the same user/milestone (separate SMS-sent marker).
- [ ] Test SMS verified in Brevo (Task 04).

## Edge Cases to Consider

- SMS-tier user with no/invalid phone → still gets all emails; no SMS, no `SmsSentCount` increment; prompt to add a number.
- Free-season rounds (World Cup): no SMS tier exists → email only (unchanged).
- Trial (Standard) users: email only (the extra SMS requires the SMS tier).
- A round whose deadline is created <6h away: the first milestone is already in the final window → the extra SMS applies immediately for eligible SMS-tier holders.

## Notes

Optional in-app "pause SMS this season" toggle (README Open Question) would short-circuit SMS for that user without a refund or inbound STOP.
