# Feature: Password Reset Flow

## Status

**Not Started** | In Progress | Complete

## Summary

Allows users to reset their password when they have forgotten it. Includes a forgot password page, secure email delivery with reset link, token expiry for security, and confirmation of successful password change. Users who signed up with Google (OAuth-only) receive a friendly email directing them to use Google sign-in instead.

## User Story

As a user who has forgotten my password, I want to request a password reset link via email so that I can regain access to my account securely.

As a Google sign-in user who tries to reset my password, I want to receive a helpful email explaining that I should use Google sign-in so that I'm not confused about why password reset doesn't work for me.

## Design / Mockup

### Forgot Password Page

```
┌─────────────────────────────────────────────────┐
│           ← Back to Login                       │
│                                                 │
│                  [Lion Logo]                    │
│                                                 │
│              Forgot Password                    │
│    Enter your email to receive a reset link    │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │ Email address                           │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │           Send Reset Link               │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Success State (Same Page)

```
┌─────────────────────────────────────────────────┐
│           ← Back to Login                       │
│                                                 │
│                  [Lion Logo]                    │
│                                                 │
│                 Check Your Email                │
│                                                 │
│    If an account exists with that email,       │
│    you'll receive a password reset link        │
│    shortly.                                     │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │           Back to Login                 │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Reset Password Page (from email link)

```
┌─────────────────────────────────────────────────┐
│                                                 │
│                  [Lion Logo]                    │
│                                                 │
│              Reset Your Password                │
│         Enter your new password below          │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │ New password                        👁  │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │ Confirm password                    👁  │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │           Reset Password                │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Invalid/Expired Token Page

```
┌─────────────────────────────────────────────────┐
│                                                 │
│                  [Lion Logo]                    │
│                                                 │
│              Link Expired                       │
│                                                 │
│    This password reset link has expired or     │
│    is invalid. Please request a new one.       │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │        Request New Link                 │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

## Behaviour

### Request Flow

1. User navigates to `/authentication/forgot-password`
2. User enters their email address
3. User clicks "Send Reset Link"
4. **Always** show success message: "If an account exists with that email, you'll receive a password reset link shortly."
5. Behind the scenes:
   - **No account exists** → Do nothing (security: don't reveal email existence)
   - **Account exists WITH password** → Generate token, store in database with user ID, send email
   - **Account exists WITHOUT password (Google-only)** → Send email explaining to use Google sign-in

### Reset Flow

1. User clicks link in email: `/authentication/reset-password?token={token}`
2. Page calls API to validate token and retrieve user
3. Token validation:
   - **Valid** → Show password reset form
   - **Invalid/Expired** → Show "Link Expired" message with option to request new link
4. User enters new password and confirms it
5. User clicks "Reset Password"
6. On success → Token deleted, auto-login and redirect to dashboard

### Security Measures

| Measure | Implementation |
|---------|----------------|
| Token expiry | 1 hour |
| Rate limiting | 3 requests per email per hour |
| Token format | Cryptographically secure random string (64 bytes, Base64 URL-safe) |
| Single use | Token deleted from database after successful reset |
| No email enumeration | Same response whether email exists or not |
| No email in URL | User lookup via token stored in database (prevents email exposure) |

## Acceptance Criteria

- [ ] Forgot password page accessible at `/authentication/forgot-password`
- [ ] "Forgot password?" link added to login page
- [ ] Email input validates format before submission
- [ ] Success message always shown regardless of email existence (security)
- [ ] Password reset email sent to users with passwords
- [ ] Google-only users receive "use Google sign-in" email instead
- [ ] Reset link expires after 1 hour
- [ ] Reset link does NOT contain email address (security)
- [ ] Reset page validates token before showing form
- [ ] Expired/invalid tokens show friendly error with link to request new one
- [ ] New password must meet existing requirements (8+ chars, upper, lower, digit, 4 unique)
- [ ] Password confirmation must match
- [ ] Token deleted after successful password reset
- [ ] Successful reset auto-logs user in and redirects to dashboard
- [ ] Rate limited to 3 requests per email per hour
- [ ] All pages match existing auth page styling

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 0 | [Manual Setup](./00-manual-setup.md) | Database table, Brevo templates, configuration | Not Started |
| 1 | [Domain & Infrastructure](./01-domain-infrastructure.md) | Create PasswordResetToken entity, repository, and IUserManager methods | Not Started |
| 2 | [Request Password Reset Command](./02-request-password-reset-command.md) | Create command to handle reset requests and store tokens | Not Started |
| 3 | [Reset Password Command](./03-reset-password-command.md) | Create command to validate token and reset password | Not Started |
| 4 | [API Endpoints](./04-api-endpoints.md) | Add endpoints to AuthController | Not Started |
| 5 | [Email Templates](./05-email-templates.md) | Document Brevo email templates to create | Not Started |
| 6 | [Forgot Password Page](./06-forgot-password-page.md) | Create Blazor forgot password page | Not Started |
| 7 | [Reset Password Page](./07-reset-password-page.md) | Create Blazor reset password page | Not Started |

## Dependencies

- [x] ASP.NET Identity already configured with `AddDefaultTokenProviders()`
- [x] Brevo email service already configured (`IEmailService`)
- [x] JWT authentication already in place
- [x] User entity has `PasswordHash` field (from IdentityUser)
- [x] Existing auth page styles available
- [ ] **Need to create:** `PasswordResetToken` entity and table
- [ ] **Need to create:** `IPasswordResetTokenRepository` and implementation
- [ ] **Need to create:** Two new Brevo email templates (see Task 5)
- [ ] **Need to add:** Two new template IDs to `TemplateSettings`

## Technical Notes

### Token Storage Approach (Security Improvement)

Instead of passing the email in the reset URL (which exposes user email in browser history and logs), we store tokens in a database table:

```
PasswordResetTokens
├── Token (PK) - Cryptographically secure random string
├── UserId (FK) - Links to the user
├── CreatedAtUtc - When token was generated
└── ExpiresAtUtc - When token expires (CreatedAtUtc + 1 hour)
```

**Benefits:**
- Email not exposed in URL, browser history, or server logs
- Token can be invalidated server-side at any time
- Easy to implement rate limiting per user
- Easy to clean up expired tokens

### Why Not ASP.NET Identity Tokens?

ASP.NET Identity's built-in `GeneratePasswordResetTokenAsync` requires the email to validate the token (it's tied to the user's SecurityStamp). By using our own token storage:
1. We avoid exposing the email in URLs
2. We have full control over token lifecycle
3. We can easily query tokens for rate limiting

### Token Generation

```csharp
// Generate a cryptographically secure token (64 bytes = 512 bits)
var tokenBytes = RandomNumberGenerator.GetBytes(64);
var token = Convert.ToBase64String(tokenBytes)
    .Replace("+", "-")
    .Replace("/", "_")
    .TrimEnd('=');  // URL-safe Base64
```

### Reset URL Format

```
https://thepredictions.co.uk/authentication/reset-password?token=abc123...
```

**Note:** No email parameter - the user is looked up from the token in the database.

### Checking for Password-Based Users

```csharp
// Returns true if user has a password hash set
bool hasPassword = await _userManager.HasPasswordAsync(user);
```

### Rate Limiting

Use the existing `auth` rate limiting policy which limits to 10 requests per 5 minutes per IP. For additional per-email limiting, check the token count in the database:

```csharp
// Check if user has requested too many resets
var recentTokenCount = await _tokenRepository.CountByUserIdSinceAsync(
    userId,
    DateTime.UtcNow.AddHours(-1)
);

if (recentTokenCount >= 3)
{
    // Rate limited - but still return success to prevent enumeration
    return Unit.Value;
}
```

### Email Template Parameters

**Password Reset Email:**
```json
{
  "firstName": "John",
  "resetLink": "https://thepredictions.co.uk/authentication/reset-password?token=abc123..."
}
```

**Google Sign-In Email:**
```json
{
  "firstName": "John",
  "loginLink": "https://thepredictions.co.uk/authentication/login"
}
```

### Cleanup Strategy

Expired tokens should be cleaned up. The simplest approach is on-demand cleanup:
- When validating a token, delete all expired tokens for that user
- This keeps the table clean without needing a background job

```csharp
// During token validation
await _tokenRepository.DeleteExpiredTokensAsync(DateTime.UtcNow);
```

## Open Questions

- [x] Token expiry time? → **1 hour**
- [x] Rate limiting? → **3 requests per email per hour**
- [x] Handle Google-only users? → **Send email directing to Google sign-in**
- [x] Password requirements? → **Use existing (8+ chars, upper, lower, digit, 4 unique)**
- [x] After successful reset? → **Auto-login and redirect to dashboard**
- [x] Email templates? → **User to create in Brevo, documented in Task 5**
- [x] Email in URL? → **No - store token→user mapping in database for security**
