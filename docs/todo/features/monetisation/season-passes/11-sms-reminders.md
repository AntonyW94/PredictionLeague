# Task: SMS Reminders

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Send deadline reminders by SMS to SMS-tier pass holders **only in the final window (6h / 1h) and only if still unsubmitted**, while emails continue free for everyone at the earlier milestones. Track how many SMS each user is sent per season.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs` | Modify | Split recipients by SMS entitlement |
| `src/ThePredictions.Application/Services/IReminderService.cs` | Modify | Expose SMS-eligible recipients |
| `src/ThePredictions.Infrastructure/Services/ReminderService.cs` | Modify | Query SMS-tier holders for the season |
| `src/ThePredictions.Application/Services/ISmsService.cs` | Create | SMS send abstraction |
| `src/ThePredictions.Infrastructure/Services/Sms/BrevoSmsService.cs` | Create | Brevo SMS implementation |

## Reminder Channel Matrix

| Milestone | Non-SMS user | SMS-tier user (still unsubmitted) |
|-----------|--------------|-----------------------------------|
| 5 days | Email | Email |
| 3 days | Email | Email |
| 1 day | Email | Email |
| **6 hours** | Email | **SMS** (not email) |
| **1 hour** | Email | **SMS** (not email) |

SMS therefore **only ever fires in the final 6 hours**, and **only if predictions are still missing**. An early submitter gets **zero** SMS that round. (Whether 1h also sends SMS is configurable — default yes; see README open question.)

## Implementation Steps

### Step 1: Identify SMS recipients (final-window only)

- A user gets **SMS instead of email** for a milestone if **all** hold:
  1. the milestone is in the **final window** (≤6 hours: i.e. the 6h and 1h milestones),
  2. they hold a `SeasonPass` with `Tier == EntryPlusSms` for the round's season,
  3. they have a valid mobile number, and
  4. they still haven't submitted predictions for the round.
- Read via `IApplicationReadDbConnection` (query side), joining `SeasonPasses` to the users missing predictions for the upcoming round.

### Step 2: Send logic (in the handler)

```
for each user missing predictions for the upcoming round at this milestone:
    if milestone is in final window (<= 6h) AND user has SMS entitlement AND valid phone:
        send SMS via ISmsService          (short, transactional text + link)
        pass.RecordSmsSent()              (increment SeasonPass.SmsSentCount, persist via repository)
    else:
        send email (existing behaviour)
```

- Keep the existing **one-message-per-user-per-round-per-milestone** de-dup and `LastReminderSentUtc` tracking so a user never gets both channels for the same milestone.
- SMS body ≤160 chars; transactional only (no promo) — see Task 04.
- Incrementing `SmsSentCount` is what powers the early-bird reward (Task 13). Note the reminder job is a **command** path, so update the count via the `SeasonPass` repository, not the read connection.

### Step 3: Brevo SMS implementation

- `BrevoSmsService.SendAsync(toNumber, message)` using the Brevo SMS API + registered sender ID. Config (API key, sender) from Key Vault/settings as per email.

## Code Patterns to Follow

Mirror the existing email reminder flow and `IEmailService` wiring. UK date formatting via existing `UkEmailDateFormatter` where shown in the link.

## Verification

- [ ] At 5d/3d/1d, **all** users (incl. SMS-tier) get email — no SMS sent early.
- [ ] At 6h/1h, an unsubmitted SMS-tier holder with a valid phone gets an SMS (not email); non-SMS users get email.
- [ ] A user who submitted before the 6h mark receives **no** SMS that round.
- [ ] `SmsSentCount` increments by exactly one per SMS actually sent.
- [ ] No double-send (email + SMS) for the same user/milestone.
- [ ] Invalid/missing phone falls back to email.
- [ ] Test SMS verified in Brevo (Task 04).

## Edge Cases to Consider

- User bought SMS tier but never added a phone → fall back to email + prompt to add number (no `SmsSentCount` increment).
- Free-season rounds (World Cup): no SMS tier exists → all email (unchanged).
- Trial (Entry) users: email only.
- A round whose deadline is created <6h away: the first eligible milestone is already in the final window → SMS applies immediately for SMS-tier holders.

## Notes

Optional in-app "pause SMS this season" toggle (README Open Question) would short-circuit SMS for that user without a refund or inbound STOP.
