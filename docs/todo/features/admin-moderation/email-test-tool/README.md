# Feature: Email Test Tool (Admin)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped (PR #103). `EmailTestsController`, the `/admin/email-tests` Blazor page, `BrevoEmailTemplateCatalog` (live discovery, 5-min cache), `EmailTestDefaultsResolver`, the CQRS layer (`GetEmailTestTemplatesQuery`, `GetEmailTestDefaultsQuery`, `SendTestEmailCommand`), and the admin nav link are all in place. All five sub-tasks complete.

## Summary

An admin-only Blazor page that lets the developer trigger any Brevo email template against their own inbox to verify content, layout and parameter wiring. Templates and their parameters are discovered live from Brevo so newly-created templates appear automatically with no code change. For known templates (e.g. password reset), smart defaults pre-fill realistic values — including a working reset-link token — so links in test emails actually function.

## User Story

As an administrator, I want to trigger any email template with realistic parameter values so that I can verify content and behaviour without having to reproduce the entire user flow each time I make a change.

## Design / Mockup

```
+-------------------------------------------------------------+
| Email Test Tool                                             |
+-------------------------------------------------------------+
|                                                             |
|  Template:  [ Password Reset             v ]  [ Refresh ]   |
|  User data: [ Antony Willson (antony@..)  v ]               |
|                                                             |
|  ----- Parameters -----                                     |
|  firstName:  [ Antony                    ]                  |
|  resetLink:  [ https://localhost:7132/.../?token=ABC...]    |
|                                                             |
|  Sends to: antony@thepredictions.co.uk (your account)       |
|  Reset link will be valid for 1 hour.                       |
|                                                             |
|                                       [ Send test email ]   |
|                                                             |
|  Last sent: 2s ago - Brevo message ID: <msg-...>            |
+-------------------------------------------------------------+
```

Inactive Brevo templates appear in the dropdown but are greyed out and selecting one shows a "Template is inactive in Brevo" warning instead of the form.

## Acceptance Criteria

- [ ] Page is reachable at `/admin/email-tests` and only visible to administrators.
- [ ] Page is linked from the existing admin cog dropdown in the navbar.
- [ ] Template dropdown lists every Brevo template (active and inactive); inactive ones are greyed out.
- [ ] Selecting a template renders one input per `{{ params.X }}` placeholder found in the template HTML.
- [ ] Inputs are pre-populated with smart defaults derived from the selected user.
- [ ] For the `Password Reset` template, `resetLink` is pre-populated with a real, working token URL.
- [ ] Submitting the form sends the email to the **calling admin's own email address**, never the selected user's email.
- [ ] The form shows the Brevo message ID on success and a clear error message on failure.
- [ ] Creating a new template in Brevo causes it to appear in the dropdown automatically (after cache expiry or manual refresh).
- [ ] Adding a new `{{ params.X }}` to an existing template causes a new input to appear automatically.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Brevo template discovery](./01-brevo-template-discovery.md) | Service that lists templates and extracts param names from HTML. 5-minute cache. | Not Started |
| 2 | [Param resolvers](./02-param-resolvers.md) | Smart defaults service. Generic name-matching + per-template overrides for password reset links. | Not Started |
| 3 | [CQRS layer](./03-cqrs-layer.md) | Queries for templates / params / smart defaults; command to send a test email. | Not Started |
| 4 | [Blazor page](./04-blazor-page.md) | The admin UI itself. Dynamic form, dropdowns, status display. | Not Started |
| 5 | [Admin cog link](./05-admin-cog-link.md) | Add the page to the admin dropdown in `NavLayout.razor`. | Not Started |

## Dependencies

- [ ] `IEmailService` and `BrevoEmailService` already exist - reused as-is.
- [ ] `IPasswordResetTokenRepository` already exists - reused for the password-reset smart default.
- [ ] Existing admin role / authorisation policy.
- [ ] Brevo's API key already loaded into config (no new secret needed).

## Technical Notes

### Why "send to caller's own email"

Using a recipient dropdown that actually sent to the chosen user would let an admin accidentally spam any user with a "password reset" or other transactional email. The user dropdown is therefore repurposed as a **data picker** - it controls whose first name, email, etc. fill the param defaults, but the actual recipient is always the calling admin (taken from the JWT, not the form).

### Discovery vs explicit registration

Templates are discovered from Brevo's API, not from `BrevoSettings.Templates`. That existing config is for *handlers* that need to refer to a template by name in code; the test page doesn't need that mapping at all. This means new templates require zero code change to be testable.

The trade-off: parameter inputs are typed only as strings (Brevo's templating is string substitution anyway, so this isn't a real loss).

### Realistic password reset links

Generating a real token requires writing a row to `[PasswordResetTokens]`. The plan handles this through the param-resolver layer (Task 2): the resolver for `Password Reset` calls into the same token-creation logic the live handler uses, so the link in the test email actually works. After 1 hour the test token expires naturally - no cleanup needed.

### Brevo API rate limits

Brevo's free plan limits API calls per second. The 5-minute in-memory cache plus a manual "Refresh templates" button on the page keeps us comfortably under any plan's limit during normal use.

### Cost

Each test send counts against the Brevo email quota (currently 300/day on the free tier). Negligible.

## Open Questions

- [ ] Should we log test sends to a `[NotificationSends]` table so they show up in the per-user cost tracking we discussed separately? Probably yes, with a `IsTest = true` flag, but out of scope for the first version.
- [ ] Should "Refresh templates" be a button or automatic on every page open? Button is simpler and avoids hammering Brevo if the page is left open.
