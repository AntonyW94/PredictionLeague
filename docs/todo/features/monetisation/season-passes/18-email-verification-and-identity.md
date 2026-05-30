# Task: Email Verification & Identity Normalisation

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Type

**Code (refactors existing auth)** — completes the half-built email-confirmation flow and hardens email uniqueness. See ADR 0020.

## Goal

Require a **verified email** before purchasing/participating, and **normalise emails** so plus-aliases (`you+tag@x.com`) can't be used to farm repeated free trials (0006).

## What exists vs. missing (from code review)

- ✅ `EmailConfirmed` column + Dapper user-store get/set; Google-login users set confirmed.
- ❌ No confirmation token, no send-on-register, no confirm endpoint, no Brevo template; login does **not** check `EmailConfirmed`.
- ❌ Uniqueness uses only `ToUpperInvariant` → plus-aliases are distinct accounts.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/EmailConfirmationToken.cs` | Create | Token model (mirror `PasswordResetToken`) |
| `...Infrastructure/Repositories/EmailConfirmationTokenRepository.cs` (+ interface) | Create | Token persistence |
| `...Features/Authentication/Commands/Register/RegisterCommandHandler.cs` | Modify | Issue + send confirmation token; normalise email for the dup check |
| `...Features/Authentication/Commands/ConfirmEmail/ConfirmEmailCommand(.Handler).cs` | Create | Validate token, set `EmailConfirmed = true` |
| `...Features/Authentication/Commands/Login/LoginCommandHandler.cs` | Modify | Gate unconfirmed users (soft or hard) |
| `src/ThePredictions.API/Controllers/AuthenticationController.cs` | Modify | `POST /api/authentication/confirm-email` (+ resend) |
| `...Application/Configuration/TemplateSettings.cs` | Modify | Add `EmailConfirmation` template id |
| Email normalisation helper (Domain/Application) | Create | Canonical key: lowercase + strip `+suffix` (optionally gmail dots) |
| `RegisterRequestValidator` / `DapperUserStore` lookups | Modify | Use the normalised key for the uniqueness check |

## Implementation Steps

### Step 1: Email confirmation (mirror password reset)

- `EmailConfirmationToken` (UserId, token, `ExpiresAtUtc`), repository, URL-safe token generation — exactly like `PasswordResetToken`/`RequestPasswordResetCommandHandler`.
- On registration: create + store a token and send via `IEmailService` using a new `EmailConfirmation` Brevo template (firstName + confirmLink).
- `ConfirmEmailCommandHandler`: look up token, check expiry, set `EmailConfirmed = true`, delete token.
- Add the confirm (and resend) endpoints.

### Step 2: Gate access on confirmation

- Require `EmailConfirmed` before **purchasing a pass / taking part** (and decide soft vs hard login gate). Google-OAuth users are already confirmed.

### Step 3: Email normalisation (block plus-aliases)

- Add a canonical-email function: trim, lowercase, and **strip `+<anything>` from the local part**. (Optionally also strip dots for `gmail.com` / `googlemail.com`.)
- Use the canonical key for the **duplicate check** at registration (and store it, e.g. a `NormalisedEmailKey`, or compute on lookup) — while still storing the **entered** email for delivery.

## Verification

- [ ] New registration receives a confirmation email; unconfirmed users can't purchase/take part.
- [ ] Confirm endpoint sets `EmailConfirmed`; expired/invalid tokens rejected.
- [ ] `you+tag@x.com` is rejected as a duplicate of `you@x.com`.
- [ ] Delivery still goes to the entered (`+`) address if used.
- [ ] Google-login users remain confirmed; existing accounts handled (grandfather/prompt).
- [ ] Domain coverage 100% for the token + normalisation logic.

## Edge Cases to Consider

- Existing unconfirmed users at rollout — grandfather or prompt to confirm (don't lock them out abruptly).
- Provider that treats `+` as distinct mailboxes — accepted over-merge (ADR 0020).
- Resend throttling to avoid email spam.

## Related

- ADR 0020; 0006 (trial abuse).
