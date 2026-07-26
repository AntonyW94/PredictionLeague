# Customer Communications Strategy

## Status

Not Started | **In Progress** | Complete

> **Verified June 2026:** Phase 1 (email design system) and Phase 2 (round-results digest, prize-won notifications, transactional emails) are shipped. Phases 3-6 (email preferences, WhatsApp/SMS channel, cost tracking) remain outstanding.

## Summary

Coordinated roadmap for everything to do with sending messages to users - email today, WhatsApp later. Captures channel-decision principles, the priority order of work, the cost ceiling, and cross-references the per-feature plans where the actual implementation detail lives.

This is the master plan. Individual features have their own folders under `docs/todo/features/email-notifications/` and (later) `docs/todo/features/whatsapp-integration/`.

## Why this exists

Right now four email templates fire from the codebase, none of them branded, and many user-impacting events send nothing at all. The user's stated pain point is that members don't read emails close to a deadline, so they currently chase via manual WhatsApp messages. We need:

1. A design system so emails actually look like the site (foundation for everything else).
2. Coverage of the high-impact missing notifications (results digest, prize won, welcome, membership approval).
3. WhatsApp as an opt-in second channel, layered on top of email - never replacing it.
4. Cost discipline: stay on Brevo's free email tier as long as possible, keep WhatsApp under £5/month.

## Channel decision principles

| Type of message | Channel | Why |
|---|---|---|
| Auth & legal (password reset, account changes) | **Email only** | Regulatory, audit, must work without a phone. |
| Time-critical retention (deadline imminent, prize won) | **Email + WhatsApp (opt-in)** | Reading the message in the next few hours is what matters. WhatsApp open rates justify the cost. |
| Routine info (round opens, results digest, league changes) | **Email only** | Useful but not urgent. Sending WhatsApp for these would feel spammy and burn money. |
| No-action FYI ("a member submitted predictions") | **Skip entirely** | Don't ping people without a reason. |

A user's phone number is **optional** (the column is nullable on `AspNetUsers`). WhatsApp is therefore always an *additional* channel for users who have opted in - never the only channel for any message type.

## Constraints to remember

- **A user's email address is immutable** in this app. There's no "your email was changed" notification path; ignore that pattern wherever it appears in industry-standard checklists.
- **Brevo email free tier:** 300 sends/day. Currently miles under it.
- **Brevo WhatsApp pricing:** roughly £0.025-£0.035 per utility template, £0.06+ for marketing. Pricing tier matters - see [WhatsApp integration plan](#) when written.
- **Meta opt-in policy:** WhatsApp recipients must explicitly opt in. Stored consent timestamp is non-negotiable.

## Cost ceiling

Target steady-state monthly cost at ~50 active users:

| Channel | Monthly volume | Cost |
|---|---|---|
| Email (welcome, results digest, deadline reminder, etc.) | ~800 sends | £0 (free tier) |
| WhatsApp deadline reminder, opted-in users only | ~120 sends | ~£4 |
| WhatsApp prize wins | ~5 sends | ~£0.20 |
| **Total** | | **~£4-5/month** |

Volumes scale linearly with user count. At 200 users we'd still be well under £20/month.

## Priority-ordered roadmap

### Phase 1 - Foundation (do first)

| # | Plan | Why first | Status |
|---|---|---|---|
| 1 | Email design system | Every email after this benefits. The current ones look basic - this is the right time to fix it. | **Complete (shipped)** |
| 2 | Admin email test tool | Makes everything below cheaper to develop and verify. | **Complete (shipped)** |

### Phase 2 - High-impact emails (do after Phase 1)

User explicitly flagged 3 and 4 as priorities.

| # | Plan | Channel | Status |
|---|---|---|---|
| 3 | Round results digest | Email only | **Complete (shipped)** |
| 4 | Prize won notification | Email shipped; WhatsApp opt-in still Phase 4 | **Complete (email)** |
| 5 | Welcome email (existing stub - to be expanded) | Email only | Stub |
| 6 | [League membership status changes](../email-notifications/league-notifications/README.md) (existing stub - to be expanded) | Email only | Stub |

For Phase 2, the prize-won WhatsApp portion is gated behind Phase 4. Build the email path first; the same handler grows a WhatsApp send when WhatsApp infra lands.

### Phase 3 - Notification preferences

| # | Plan | Why | Status |
|---|---|---|---|
| 7 | [Notification preferences page](../email-notifications/email-preferences/README.md) (existing stub - to be expanded) | Foundation for WhatsApp opt-in. Phone number capture lives here. Per-message-type granularity. | Stub |

### Phase 4 - WhatsApp infrastructure

| # | Plan | Why | Status |
|---|---|---|---|
| 8 | WhatsApp integration foundation (folder to be created) | `IWhatsAppService` mirroring `IEmailService`, Brevo BSP onboarding, Meta Business Account setup, first approved utility template, opt-in plumbing through the preferences page. | Not Started |

### Phase 5 - Channel layering

| # | Plan | Status |
|---|---|---|
| 9 | Prediction reminder - the email redesign shipped (2026-07-25, plan removed); the remaining comms work is extending it to send WhatsApp at T-3h to opted-in users alongside the email | WhatsApp extension not started |
| 10 | Add WhatsApp send to prize-won notification (re-uses Phase 4 infra) | Not Started |

### Phase 6 - Observability

| # | Plan | Status |
|---|---|---|
| 11 | `[NotificationSends]` table + per-user cost rollup (foundation for charging users later) | Not Started |

### Out of scope (for now)

- New round published email - low value, skip.
- Predictions submitted confirmation email - UI feedback is enough, would just clutter inboxes.
- League deleted email - rare event, build only if it becomes an actual issue.
- "Email changed" notification - cannot happen by design.

## How this hangs together

```
Phase 1: Design system + test tool
                |
                v
Phase 2: Welcome / Membership / Results digest / Prize email
                |
                v
Phase 3: Notification preferences page (incl. phone capture, opt-ins)
                |
                v
Phase 4: WhatsApp infra
                |
                v
Phase 5: Add WhatsApp channel to existing notifications (deadline, prize)
                |
                v
Phase 6: Cost tracking
```

Each phase unblocks the next. You can ship Phases 1-2 without ever touching WhatsApp and have a much better-looking, more complete email experience first.

## Open questions

- [ ] Should plain-text fallbacks be auto-generated by Brevo or hand-written? (Hand-written gives more control over how the email reads with images blocked.)
- [ ] Should WhatsApp be opted-in per message type (deadline yes / prize no) or just a single global toggle? Single toggle is simpler and matches small-app expectations.
- [ ] Do we ever want SMS as a fallback for users who give a phone number but don't use WhatsApp? Probably not - WhatsApp + email covers the realistic universe of users.
- [ ] Should the design system live as Brevo template fragments (Brevo supports template inheritance via "blocks") or as raw HTML duplicated across each template? Worth investigating during Phase 1.

## Related

- Admin email test tool - shipped; the testing harness all these new templates needed.
- [Password reset feature](../authentication/password-reset/README.md) - already shipped, the reference pattern for new email-sending features.
