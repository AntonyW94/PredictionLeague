# Feature: Prediction Reminder Email Redesign

## Status

**Not Started** | In Progress | Complete

## Summary

Redesign the prediction reminder email to be visually engaging and drive action. The existing reminder system already works (cron job, milestone timing, user targeting). This feature enriches the data sent to Brevo and provides a template design that uses conditional content to vary urgency based on time remaining.

## What Exists Today

### Reminder System (No Changes Needed)

- Cron job calls `POST /api/tasks/send-reminders` every 30 minutes
- `ReminderService.ShouldSendReminderAsync` checks hardcoded milestones: **5 days, 3 days, 1 day, 6 hours, 1 hour** before deadline
- `GetUsersMissingPredictionsAsync` finds users across all leagues who haven't submitted predictions for the upcoming round
- One email per user per round (not per league) -- avoids spam for users in multiple leagues
- `LastReminderSentUtc` tracked per round to prevent duplicate sends

### Current Template Parameters (Brevo Template #2)

| Parameter | Example Value |
|-----------|---------------|
| `FIRST_NAME` | `Antony` |
| `ROUND_NAME` | `Round 5` |
| `DEADLINE` | `Friday, 07 February 2026 at 19:00 (GMT)` |

That's it. No link, no urgency, no personality.

## What Changes

### Code Changes (Small)

Add three new parameters to the Brevo template payload. No new tables, no schema changes, no new endpoints.

| New Parameter | Type | Example | Purpose |
|---------------|------|---------|---------|
| `PREDICTIONS_URL` | string | `https://www.thepredictions.co.uk/predictions/5` | Direct link for the CTA button |
| `URGENCY` | string | `relaxed` / `soon` / `urgent` | Drives conditional content in Brevo |
| `TIME_REMAINING` | string | `3 days` / `6 hours` / `45 minutes` | Human-readable countdown for display |

### Brevo Template (Manual)

Replace template #2 with a redesigned template using Brevo's drag-and-drop editor + conditional content blocks.

## New Template Parameters (Full Set)

After the code change, the template will receive:

```json
{
  "FIRST_NAME": "Antony",
  "ROUND_NAME": "Round 5",
  "DEADLINE": "Friday, 07 February 2026 at 19:00 (GMT)",
  "PREDICTIONS_URL": "https://www.thepredictions.co.uk/predictions/5",
  "URGENCY": "soon",
  "TIME_REMAINING": "6 hours"
}
```

### URGENCY Tiers

| Tier | Condition | Tone |
|------|-----------|------|
| `relaxed` | More than 24 hours remaining | Friendly nudge |
| `soon` | 6 to 24 hours remaining | Don't miss out |
| `urgent` | Less than 6 hours remaining | Last chance |

## Email Content

### Subject Line

Use Brevo conditional syntax in the template subject:

```
{% if params.URGENCY == "urgent" %}Only {{ params.TIME_REMAINING }} left to submit your predictions!{% elif params.URGENCY == "soon" %}Don't forget to submit your {{ params.ROUND_NAME }} predictions{% else %}{{ params.ROUND_NAME }} predictions are open{% endif %}
```

### Email Body Structure

```
┌──────────────────────────────────────────────────────────┐
│                                                          │
│                     [Site Logo]                           │
│              The Predictions League                       │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  URGENCY BANNER (conditional -- urgent/soon only)        │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Hey {FIRST_NAME},                                       │
│                                                          │
│  MAIN MESSAGE (varies by urgency tier)                   │
│                                                          │
│  ┌──────────────────────────────────────────────────┐    │
│  │                                                  │    │
│  │          SUBMIT YOUR PREDICTIONS                 │    │
│  │               [CTA Button]                       │    │
│  │                                                  │    │
│  └──────────────────────────────────────────────────┘    │
│                                                          │
│  Deadline: {DEADLINE}                                    │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Footer: The Predictions League                          │
│  thepredictions.co.uk                                    │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### Content by Urgency Tier

#### Relaxed (more than 24 hours)

**Banner:** None

**Body:**

> Hey {{ params.FIRST_NAME }},
>
> {{ params.ROUND_NAME }} predictions are open and the deadline is approaching. You've got **{{ params.TIME_REMAINING }}** left to get your scores in.
>
> **[Submit Your Predictions]**
>
> Deadline: {{ params.DEADLINE }}

---

#### Soon (6 to 24 hours)

**Banner:** Amber/yellow background

> Predictions close in {{ params.TIME_REMAINING }}

**Body:**

> Hey {{ params.FIRST_NAME }},
>
> Time's ticking! You haven't submitted your predictions for {{ params.ROUND_NAME }} yet and the deadline is **{{ params.TIME_REMAINING }}** away.
>
> Don't miss out -- get your scores in now.
>
> **[Submit Your Predictions]**
>
> Deadline: {{ params.DEADLINE }}

---

#### Urgent (less than 6 hours)

**Banner:** Red background

> Only {{ params.TIME_REMAINING }} left!

**Body:**

> Hey {{ params.FIRST_NAME }},
>
> This is your last chance! {{ params.ROUND_NAME }} predictions close in just **{{ params.TIME_REMAINING }}**.
>
> Submit now or you'll miss this round entirely.
>
> **[Submit Your Predictions]**
>
> Deadline: {{ params.DEADLINE }}

## Brevo Template Implementation

### Conditional Content (Jinja Syntax)

Brevo templates support Jinja-based `{% if %}` blocks that work with `params.*` variables passed via the API. Use these in the template HTML:

```html
<!-- Urgency banner: only shown for "soon" and "urgent" -->
{% if params.URGENCY == "urgent" %}
<div style="background-color: #dc3545; color: white; padding: 12px; text-align: center; font-weight: bold; font-size: 18px;">
  Only {{ params.TIME_REMAINING }} left!
</div>
{% elif params.URGENCY == "soon" %}
<div style="background-color: #ffc107; color: #333; padding: 12px; text-align: center; font-weight: bold; font-size: 18px;">
  Predictions close in {{ params.TIME_REMAINING }}
</div>
{% endif %}

<!-- Main message body varies by urgency -->
{% if params.URGENCY == "urgent" %}
<p>This is your last chance! {{ params.ROUND_NAME }} predictions close in just <strong>{{ params.TIME_REMAINING }}</strong>.</p>
<p>Submit now or you'll miss this round entirely.</p>
{% elif params.URGENCY == "soon" %}
<p>Time's ticking! You haven't submitted your predictions for {{ params.ROUND_NAME }} yet and the deadline is <strong>{{ params.TIME_REMAINING }}</strong> away.</p>
<p>Don't miss out &ndash; get your scores in now.</p>
{% else %}
<p>{{ params.ROUND_NAME }} predictions are open and the deadline is approaching. You've got <strong>{{ params.TIME_REMAINING }}</strong> left to get your scores in.</p>
{% endif %}
```

### CTA Button

```html
<a href="{{ params.PREDICTIONS_URL }}"
   style="display: inline-block; background-color: #6f42c1; color: white; padding: 14px 32px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;">
  Submit Your Predictions
</a>
```

### Drag-and-Drop vs Code Editor

If you prefer the Brevo drag-and-drop editor over raw HTML:

1. Create a **Section** for the urgency banner
2. Use **Content Visibility** > Add Condition on that section
3. However, Content Visibility conditions only work with **contact attributes**, not `params`
4. For `params`-based conditions, you must use the **code editor** (HTML mode) with `{% if %}` blocks

**Recommendation:** Use the drag-and-drop editor for overall layout and styling, then switch to the code view to wrap the relevant sections with `{% if %}` conditionals.

## Tasks

| # | Task | Description | Type |
|---|------|-------------|------|
| 1 | [Code Changes](./01-code-changes.md) | Add `PREDICTIONS_URL`, `URGENCY`, and `TIME_REMAINING` to the handler | Code |
| 2 | Brevo Template | Design and build the template in Brevo's editor | Manual |
| 3 | Test | Send test emails for each urgency tier, verify links and conditional content | Manual |

## Dependencies

- [x] Reminder system already working (cron job, milestones, user targeting)
- [x] Brevo email service configured (`IEmailService`, `BrevoSettings`)
- [x] Template #2 (`PredictionsMissing`) already registered in config
- [x] UK date formatter already in place (`UkEmailDateFormatter`)
- [ ] **Need:** Brevo template redesigned (manual -- Task 2)

## Affected Files

| File | Change |
|------|--------|
| `SendScheduledRemindersCommandHandler.cs` | Add new parameters to the template payload |
| `BrevoSettings` / `appsettings.json` | Add `SiteBaseUrl` setting (for building the predictions URL) |

## Design Notes

### Why Not Per-League Configurable Timing?

The current system sends **one email per user per round**, regardless of how many leagues the user is in. Making reminder timing per-league would either:
- Spam users with multiple emails per round (one per league), or
- Require complex deduplication logic with confusing "most aggressive wins" behaviour

The existing global milestones (5d, 3d, 1d, 6h, 1h) are sensible defaults. The real value add is the email quality, not the timing.

### Why Pass URGENCY as a String?

Brevo warns against using `{% if %}` with float values. By calculating the urgency tier server-side and passing a simple string (`relaxed` / `soon` / `urgent`), we avoid Brevo template bugs and keep the template logic clean.

### Predictions URL

The Blazor app route is `/predictions/{RoundId}`. The handler already has access to `nextRound.Id`, so the URL is straightforward:

```
{SiteBaseUrl}/predictions/{nextRound.Id}
```

A `SiteBaseUrl` setting is needed in config since the handler runs server-side and doesn't know the client URL.

## Open Questions

- [ ] Should the CTA button colour match the urgency tier? (Purple default, amber for "soon", red for "urgent")
- [ ] Should the email include the site logo? If so, host the image and provide the URL
- [ ] Footer content -- include an unsubscribe line? (Even if non-functional for now, it's good practice for email deliverability)
