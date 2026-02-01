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
   - **Account exists WITH password** → Send password reset email with unique token
   - **Account exists WITHOUT password (Google-only)** → Send email explaining to use Google sign-in

### Reset Flow

1. User clicks link in email: `/authentication/reset-password?token={token}&email={email}`
2. Page validates token:
   - **Valid** → Show password reset form
   - **Invalid/Expired** → Show "Link Expired" message with option to request new link
3. User enters new password and confirms it
4. User clicks "Reset Password"
5. On success → Auto-login and redirect to dashboard

### Security Measures

| Measure | Implementation |
|---------|----------------|
| Token expiry | 1 hour |
| Rate limiting | 3 requests per email per hour |
| Token format | ASP.NET Identity token (cryptographically secure) |
| Single use | Token invalidated after successful reset |
| No email enumeration | Same response whether email exists or not |

## Acceptance Criteria

- [ ] Forgot password page accessible at `/authentication/forgot-password`
- [ ] "Forgot password?" link added to login page
- [ ] Email input validates format before submission
- [ ] Success message always shown regardless of email existence (security)
- [ ] Password reset email sent to users with passwords
- [ ] Google-only users receive "use Google sign-in" email instead
- [ ] Reset link expires after 1 hour
- [ ] Reset page validates token before showing form
- [ ] Expired/invalid tokens show friendly error with link to request new one
- [ ] New password must meet existing requirements (8+ chars, upper, lower, digit, 4 unique)
- [ ] Password confirmation must match
- [ ] Successful reset auto-logs user in and redirects to dashboard
- [ ] Rate limited to 3 requests per email per hour
- [ ] All pages match existing auth page styling

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Domain & Infrastructure](./01-domain-infrastructure.md) | Add password reset methods to IUserManager | Not Started |
| 2 | [Request Password Reset Command](./02-request-password-reset-command.md) | Create command to handle reset requests | Not Started |
| 3 | [Reset Password Command](./03-reset-password-command.md) | Create command to handle password reset | Not Started |
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
- [ ] **Need to create:** Two new Brevo email templates (see Task 5)
- [ ] **Need to add:** Two new template IDs to `TemplateSettings`

## Technical Notes

### ASP.NET Identity Password Reset Tokens

ASP.NET Identity provides built-in token generation for password reset:

```csharp
// Generate token (valid for configured duration)
var token = await _userManager.GeneratePasswordResetTokenAsync(user);

// Reset password with token
var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
```

The token is:
- Cryptographically secure (HMACSHA256)
- URL-safe when encoded
- Automatically invalidated after use
- Tied to user's `SecurityStamp` (changes when password changes)

### Token Expiry Configuration

Add to Identity configuration in `DependencyInjection.cs`:

```csharp
services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});
```

### Checking for Password-Based Users

```csharp
// Returns true if user has a password hash set
bool hasPassword = await _userManager.HasPasswordAsync(user);
```

### Rate Limiting

Use the existing `auth` rate limiting policy which limits to 10 requests per 5 minutes per IP. For additional per-email limiting, track requests in memory or database.

### Email Template Parameters

**Password Reset Email:**
```json
{
  "firstName": "John",
  "resetLink": "https://thepredictions.co.uk/authentication/reset-password?token=xxx&email=xxx"
}
```

**Google Sign-In Email:**
```json
{
  "firstName": "John",
  "loginLink": "https://thepredictions.co.uk/authentication/login"
}
```

## Open Questions

- [x] Token expiry time? → **1 hour**
- [x] Rate limiting? → **3 requests per email per hour**
- [x] Handle Google-only users? → **Send email directing to Google sign-in**
- [x] Password requirements? → **Use existing (8+ chars, upper, lower, digit, 4 unique)**
- [x] After successful reset? → **Auto-login and redirect to dashboard**
- [x] Email templates? → **User to create in Brevo, documented in Task 5**
