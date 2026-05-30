# Task: SMS Reminders

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Send deadline reminders by SMS to SMS-tier pass holders, while everyone else continues to receive the existing email reminder.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs` | Modify | Split recipients by SMS entitlement |
| `src/ThePredictions.Application/Services/IReminderService.cs` | Modify | Expose SMS-eligible recipients |
| `src/ThePredictions.Infrastructure/Services/ReminderService.cs` | Modify | Query SMS-tier holders for the season |
| `src/ThePredictions.Application/Services/ISmsService.cs` | Create | SMS send abstraction |
| `src/ThePredictions.Infrastructure/Services/Sms/BrevoSmsService.cs` | Create | Brevo SMS implementation |

## Implementation Steps

### Step 1: Identify SMS recipients

- For the round's season, a user gets SMS if they hold a `SeasonPass` with `Tier == EntryPlusSms` **and** have a valid mobile number.
- Read via `IApplicationReadDbConnection` (query side), joining `SeasonPasses` to the users missing predictions.

### Step 2: Send logic (in the handler)

```
for each user missing predictions for the upcoming round:
    if user has SMS entitlement for that season AND valid phone:
        send SMS via ISmsService  (short, transactional text + link)
    else:
        send email (existing behaviour)
```

- Keep the existing **one-message-per-user-per-round** de-dup and `LastReminderSentUtc` tracking.
- SMS body ≤160 chars; transactional only (no promo) — see Task 04.

### Step 3: Brevo SMS implementation

- `BrevoSmsService.SendAsync(toNumber, message)` using the Brevo SMS API + registered sender ID. Config (API key, sender) from Key Vault/settings as per email.

## Code Patterns to Follow

Mirror the existing email reminder flow and `IEmailService` wiring. UK date formatting via existing `UkEmailDateFormatter` where shown in the link.

## Verification

- [ ] SMS-tier holder with valid phone receives an SMS; non-SMS users still get email.
- [ ] No double-send (email + SMS) to the same user for the same round.
- [ ] Invalid/missing phone falls back to email.
- [ ] Test SMS verified in Brevo (Task 04).

## Edge Cases to Consider

- User bought SMS tier but never added a phone → fall back to email + prompt to add number.
- Free-season rounds (World Cup): no SMS tier exists → all email (unchanged).
- Trial (Entry) users: email only.

## Notes

Optional in-app "pause SMS this season" toggle (README Open Question) would short-circuit SMS for that user without a refund or inbound STOP.
