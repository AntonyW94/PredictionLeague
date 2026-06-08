# Email Templates

Transactional emails are sent via **Brevo** (see `BrevoEmailService`). Brevo holds the live
templates; the `.html` files here are the **source-of-truth copies** so we have them in version
control and a consistent house style to build new ones from. When you change a template in Brevo,
update the matching file here (and vice versa).

Each email is sent by a handler that passes a `params` object to Brevo; the template references those
values as `{{ params.NAME }}`. **The casing must match the handler exactly** — a mismatch renders blank.
The template id for each is configured under `Brevo:Templates` in
[`src/ThePredictions.Web/appsettings.json`](../../src/ThePredictions.Web/appsettings.json).

## Index

| Template | File | Brevo id | Trigger (handler) | Merge tags (`{{ params.X }}`) | CTA link |
|----------|------|---------|-------------------|-------------------------------|----------|
| Join League Request | [join-league-request.html](join-league-request.html) | 10 | `NotifyLeagueAdminOfJoinRequestCommandHandler` | `ADMIN_NAME`, `FIRST_NAME`, `LAST_NAME`, `LEAGUE_NAME`, `SEASON_NAME`, `DASHBOARD_URL` | `{{ params.DASHBOARD_URL }}` → `/dashboard?tab=admin` |
| Predictions Missing | [predictions-missing.html](predictions-missing.html) | 2 *(migrating to a new HTML template, id 9)* | `SendScheduledRemindersCommandHandler` | `FIRST_NAME`, `ROUND_NAME`, `DEADLINE`, `PREDICTIONS_URL` | `{{ params.PREDICTIONS_URL }}` → `/predictions/{roundId}` |
| League Join Approved | [league-join-approved.html](league-join-approved.html) | 5 | `NotifyMemberOfLeagueApprovalCommandHandler` | `FIRST_NAME`, `LEAGUE_NAME`, `SEASON_NAME`, `LEAGUE_URL` | `{{ params.LEAGUE_URL }}` → `/leagues/{id}/dashboard` |
| Email Confirmation | [email-confirmation.html](email-confirmation.html) | 6 | `EmailConfirmationSender` | `firstName`, `confirmLink` | `{{ params.confirmLink }}` |
| Password Reset – Google User | [password-reset-google-user.html](password-reset-google-user.html) | 7 | `RequestPasswordResetCommandHandler` | `firstName`, `loginLink` | `{{ params.loginLink }}` |
| Password Reset | [password-reset.html](password-reset.html) | 8 | `RequestPasswordResetCommandHandler` | `firstName`, `resetLink` | `{{ params.resetLink }}` |

> ⚠️ **Tag casing varies by template** (legacy reasons): account/auth emails use lowercase
> (`firstName`, `confirmLink`, `resetLink`, `loginLink`); league/round emails use `UPPER_SNAKE`.
> Always copy the exact names from the handler.

## Link building (no hard-coded domain in templates)

CTA links are passed in as params so the right environment/URL is used:

- **HTTP-triggered emails** (join request, league approval) derive the base from the request `Origin`,
  threaded through the command as `LeagueUrlBase` (same pattern as the confirmation/reset emails).
- **Background-job emails** (predictions reminder) have no request, so they use `SiteSettings.BaseUrl`
  (bound from `ApiBaseUrl` in `Program.cs`).
- Handlers fall back to `https://www.thepredictions.co.uk` if the base is missing, so a button always
  has a working destination.

Note `www.thepredictions.co.uk` is canonical — the app redirects the apex to `www` (see `Program.cs`).

## House style (use this for every new template)

Copy an existing file and keep these conventions:

1. **Hidden preheader first** — controls the inbox snippet; end it with zero-width spaces (`&#8203;`)
   so body text doesn't bleed in.
2. **Outer wrapper:** full-width table, `background:#F8F5FA`, `padding:32px 12px`,
   font `'Segoe UI',Arial,Helvetica,sans-serif`.
3. **Card:** `width:100%; max-width:600px` (never a fixed `width="600"` — that causes mobile
   horizontal scroll), `border:1px solid #F0EAF5`, `border-radius:16px`, soft shadow.
4. **Outlook lock:** wrap the card in the MSO ghost table
   (`<!--[if mso]><table width="600">…<![endif]-->`) since Outlook ignores `max-width`.
5. **Header:** purple gradient `linear-gradient(135deg,#3D195B 0%,#2C0A3D 100%)`, with the
   logo + "The Predictions" wordmark lockup (logo `alt=""` because the wordmark text is present).
6. **Hero:** a pill + an `<h1>` in `#2C0A3D`.
7. **Info panel** for key facts: `background:#F8F5FA; border:1px solid #F0EAF5; border-radius:12px`,
   uppercase grey labels (`#98a2b3`) + `#2C0A3D` values.
8. **Bulletproof button:** anchor inside a `bgcolor="#3D195B"` cell with `border-radius:10px`
   (Outlook strips `background` on anchors, so the cell colour matters).
9. **Footer:** Privacy · Terms · Cookie Policy (links `#5D3E85`), a context line, `© The Predictions`.
10. **Colours = light-theme tokens:** `--purple-1000 #2C0A3D`, `--purple-800 #3D195B`,
    `--purple-500 #5D3E85`, `--purple-100 #F0EAF5`, `--purple-50 #F8F5FA`, `--green-600 #00B960`.
    Body copy stays neutral grey `#475467`.
11. **Hyphens, not em dashes.** Sender name is **The Predictions**.

To preview/test in Brevo: open **Preview & test**, toggle **Add transactional JSON data**, and paste a
`{"params":{…}}` object with the tags above (hidden preheaders show blank in the preview pane — that's
expected; use **Send test email** to verify the inbox snippet).
