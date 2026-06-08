# Process: Brevo Email Template Management

## Key point

**Brevo transactional email templates are managed programmatically via the Brevo API - do NOT
hand-author them in the Brevo web UI.** Anyone (developer or AI assistant) with the local dev
secrets file can read the Brevo API key and create, update, list, and send templates directly.
Template work therefore needs no manual UI steps.

The HTML source of truth for each template lives in [`docs/email-templates/`](../email-templates/);
Brevo holds the deployed copy. Keep the two in sync.

## How the API key is accessed

The key is **never** in the repo. It lives in Azure Key Vault and is referenced from the
committed `appsettings.json` as `"ApiKey": "${Brevo-ApiKey}"`.

- Vault: `the-predictions-dev` (`https://the-predictions-dev.vault.azure.net/`), secret `Brevo-ApiKey`.
- The app authenticates to the vault with a **service principal** whose credentials are in the
  gitignored `src/ThePredictions.Web/appsettings.Development.Secrets.json` (`AzureCredentials`:
  `TenantId` / `ClientId` / `ClientSecret`). Only authorised developers have this file.
- Note: an interactive `az login` under the `@evolution-internet.com` tenant cannot read this
  vault (different tenant - `AKV10032 Invalid issuer`). Use the service principal above.

### Retrieve the key (PowerShell)

Reads from the local gitignored secrets file. Keeps the key in a variable; never print it.

```powershell
$t = (Get-Content "src\ThePredictions.Web\appsettings.Development.Secrets.json" -Raw | ConvertFrom-Json).AzureCredentials
$tok = (Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$($t.TenantId)/oauth2/v2.0/token" -Body @{ client_id=$t.ClientId; client_secret=$t.ClientSecret; scope="https://vault.azure.net/.default"; grant_type="client_credentials" } -ContentType "application/x-www-form-urlencoded").access_token
$brevoKey = (Invoke-RestMethod -Method Get -Uri "https://the-predictions-dev.vault.azure.net/secrets/Brevo-ApiKey?api-version=7.4" -Headers @{ Authorization = "Bearer $tok" }).value
$h = @{ "api-key" = $brevoKey; "accept" = "application/json"; "content-type" = "application/json" }
```

## Brevo API capabilities

With header `api-key: <brevoKey>`:

| Action | Call |
|--------|------|
| List templates | `GET https://api.brevo.com/v3/smtp/templates?limit=1000` |
| Get one | `GET .../v3/smtp/templates/{id}` |
| Create | `POST .../v3/smtp/templates` (templateName, subject, htmlContent, sender, isActive, tag) |
| Update | `PUT .../v3/smtp/templates/{id}` |
| Send | `POST .../v3/smtp/email` (sends a real email) |

## Conventions

- **Verified sender:** only one exists - `The Predictions <antony@thepredictions.co.uk>` (id `1`).
  Attach it to new templates.
- **Single shared Brevo account.** Template IDs are environment-agnostic (the same numbers appear
  in every `appsettings`), so a template created with the dev key is immediately present for
  production too.
- **Do not modify the existing live templates** (currently ids 5-10: League Join Approved,
  Confirm Email, Password Reset, Password Reset - Google User, Predictions Missing, Join League
  Request) without explicit sign-off.
- **Create new templates as `isActive: true`** - the admin email-test tool (`/admin/email-tests`)
  greys out inactive templates, so an inactive one cannot be tested. Nothing actually sends to
  users until a code path and its scheduled task exist.
- **Never commit the API key or any token.**

## Standard workflow for a new template

1. Author the HTML in the repo under `docs/email-templates/<name>.html` (version-controlled).
2. Create it via the API (`POST`); capture the returned template ID.
3. Preview it via the admin email-test tool, or an API-level send to your own inbox.
4. Refine via the API (`PUT`) until it renders correctly across clients.
5. Wire the ID into `BrevoSettings.Templates.<Name>` in `appsettings.json` and reference it from
   the relevant handler.

## Related

- Admin email-test tool: `/admin/email-tests` - discovers templates and their `{{ params.X }}`
  merge tags live from Brevo and sends a test to the signed-in admin.
- Email house style and stored template copies: [`docs/email-templates/`](../email-templates/).
