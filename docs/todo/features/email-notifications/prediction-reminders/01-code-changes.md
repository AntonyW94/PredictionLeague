# Task 1: Code Changes

## Status

**Not Started** | In Progress | Complete

## Summary

Add `PREDICTIONS_URL`, `URGENCY`, and `TIME_REMAINING` parameters to the prediction reminder email payload. This is a small change to the existing `SendScheduledRemindersCommandHandler`.

## Changes Required

### 1. Add `SiteBaseUrl` to `BrevoSettings`

**File:** `PredictionLeague.Application/Configuration/BrevoSettings.cs`

```csharp
public class BrevoSettings
{
    public string? ApiKey { get; init; }
    public string? SendFromName { get; init; }
    public string? SendFromEmail { get; init; }
    public string? SiteBaseUrl { get; init; }    // NEW
    public TemplateSettings? Templates { get; init; }
}
```

### 2. Add `SiteBaseUrl` to appsettings

**File:** `PredictionLeague.Web/PredictionLeague.Web/appsettings.json`

```json
"Brevo": {
    "ApiKey": "${Brevo-ApiKey}",
    "SendFromName": "The Predictions League",
    "SendFromEmail": "antony@thepredictions.co.uk",
    "SiteBaseUrl": "https://www.thepredictions.co.uk",
    "Templates": {
        "JoinLeagueRequest": 1,
        "PredictionsMissing": 2
    }
}
```

**File:** `PredictionLeague.Web/PredictionLeague.Web/appsettings.Development.json`

Add `SiteBaseUrl` pointing to `https://localhost:7132` (or whatever the local dev URL is).

### 3. Update `SendScheduledRemindersCommandHandler`

**File:** `PredictionLeague.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs`

Replace the parameters block in the `foreach` loop:

```csharp
foreach (var user in usersToChase)
{
    var hoursRemaining = (user.DeadlineUtc - nowUtc).TotalHours;

    var parameters = new
    {
        FIRST_NAME = user.FirstName,
        ROUND_NAME = user.RoundName,
        DEADLINE = _dateFormatter.FormatDeadline(user.DeadlineUtc),
        PREDICTIONS_URL = $"{_brevoSettings.SiteBaseUrl}/predictions/{nextRound.Id}",
        URGENCY = hoursRemaining switch
        {
            < 6 => "urgent",
            < 24 => "soon",
            _ => "relaxed"
        },
        TIME_REMAINING = FormatTimeRemaining(user.DeadlineUtc, nowUtc)
    };

    await _emailService.SendTemplatedEmailAsync(user.Email, templateId.Value, parameters);

    _logger.LogInformation("Sent chase notification for Round (ID: {RoundId}) to User (ID: {UserId})", nextRound.Id, user.UserId);
}
```

### 4. Add `FormatTimeRemaining` helper method

Add a private method to the handler:

```csharp
private static string FormatTimeRemaining(DateTime deadlineUtc, DateTime nowUtc)
{
    var remaining = deadlineUtc - nowUtc;

    if (remaining.TotalDays >= 1)
    {
        var days = (int)remaining.TotalDays;
        return days == 1 ? "1 day" : $"{days} days";
    }

    if (remaining.TotalHours >= 1)
    {
        var hours = (int)remaining.TotalHours;
        return hours == 1 ? "1 hour" : $"{hours} hours";
    }

    var minutes = Math.Max(1, (int)remaining.TotalMinutes);
    return minutes == 1 ? "1 minute" : $"{minutes} minutes";
}
```

## Verification

After deploying the code change, verify via logging that the new parameters appear:

1. Trigger the reminder endpoint manually: `POST /api/tasks/send-reminders`
2. Check application logs for the sent email confirmation
3. In Brevo dashboard, check the email activity to confirm parameters are being received
4. Send a test email to yourself at each urgency tier by adjusting the round deadline in dev

## Notes

- The `SiteBaseUrl` should **not** have a trailing slash
- `FormatTimeRemaining` uses integer truncation (e.g. 2.7 hours = "2 hours") which reads more naturally than rounding
- The `URGENCY` switch expression matches the tier boundaries: urgent < 6h, soon < 24h, relaxed >= 24h
- No changes needed to `ChaseUserDto`, `IReminderService`, `ReminderService`, or any other existing files
