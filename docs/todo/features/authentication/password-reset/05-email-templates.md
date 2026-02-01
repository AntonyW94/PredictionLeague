# Task 5: Email Templates

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Document the two Brevo email templates that need to be created manually in the Brevo dashboard, and configure their template IDs in the application settings.

## Templates to Create in Brevo

You need to create **two** email templates in your Brevo account:

| Template | Purpose | When Sent |
|----------|---------|-----------|
| Password Reset | Contains reset link | User with password requests reset |
| Google Sign-In Reminder | Explains to use Google | Google-only user requests reset |

---

## Template 1: Password Reset Email

### Template Details

| Setting | Value |
|---------|-------|
| Template Name | `Password Reset` |
| Subject | `Reset your password - The Predictions` |
| From Name | `The Predictions` (or your configured sender name) |
| From Email | Your configured sender email |

### Template Parameters

The following parameters will be passed from the application:

| Parameter | Type | Example |
|-----------|------|---------|
| `firstName` | string | `"John"` |
| `resetLink` | string | `"https://thepredictions.co.uk/authentication/reset-password?token=xxx&email=xxx"` |

### Suggested Email Content

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Reset Your Password</title>
</head>
<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;">
    <div style="text-align: center; margin-bottom: 30px;">
        <img src="YOUR_LOGO_URL" alt="The Predictions" style="max-width: 150px;">
    </div>

    <h1 style="color: #1a1a2e; text-align: center;">Reset Your Password</h1>

    <p>Hi {{ params.firstName }},</p>

    <p>We received a request to reset your password for your The Predictions account.</p>

    <p>Click the button below to set a new password:</p>

    <div style="text-align: center; margin: 30px 0;">
        <a href="{{ params.resetLink }}"
           style="background-color: #4ade80; color: #1a1a2e; padding: 14px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;">
            Reset Password
        </a>
    </div>

    <p style="color: #666; font-size: 14px;">
        This link will expire in <strong>1 hour</strong> for security reasons.
    </p>

    <p style="color: #666; font-size: 14px;">
        If you didn't request this password reset, you can safely ignore this email. Your password will remain unchanged.
    </p>

    <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">

    <p style="color: #999; font-size: 12px; text-align: center;">
        If the button doesn't work, copy and paste this link into your browser:<br>
        <a href="{{ params.resetLink }}" style="color: #666; word-break: break-all;">{{ params.resetLink }}</a>
    </p>

    <p style="color: #999; font-size: 12px; text-align: center;">
        © 2024 The Predictions. All rights reserved.
    </p>
</body>
</html>
```

### Plain Text Version

```
Hi {{ params.firstName }},

We received a request to reset your password for your The Predictions account.

Click the link below to set a new password:
{{ params.resetLink }}

This link will expire in 1 hour for security reasons.

If you didn't request this password reset, you can safely ignore this email. Your password will remain unchanged.

---
© 2024 The Predictions. All rights reserved.
```

---

## Template 2: Google Sign-In Reminder Email

### Template Details

| Setting | Value |
|---------|-------|
| Template Name | `Password Reset - Google User` |
| Subject | `Sign in with Google - The Predictions` |
| From Name | `The Predictions` (or your configured sender name) |
| From Email | Your configured sender email |

### Template Parameters

| Parameter | Type | Example |
|-----------|------|---------|
| `firstName` | string | `"John"` |
| `loginLink` | string | `"https://thepredictions.co.uk/authentication/login"` |

### Suggested Email Content

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Sign in with Google</title>
</head>
<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;">
    <div style="text-align: center; margin-bottom: 30px;">
        <img src="YOUR_LOGO_URL" alt="The Predictions" style="max-width: 150px;">
    </div>

    <h1 style="color: #1a1a2e; text-align: center;">Sign in with Google</h1>

    <p>Hi {{ params.firstName }},</p>

    <p>We received a request to reset your password, but your account is set up to sign in with Google.</p>

    <p>You don't have a password to reset! Instead, please use the <strong>"Continue with Google"</strong> button on our login page to access your account.</p>

    <div style="text-align: center; margin: 30px 0;">
        <a href="{{ params.loginLink }}"
           style="background-color: #4285f4; color: white; padding: 14px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;">
            Go to Login Page
        </a>
    </div>

    <p style="color: #666; font-size: 14px;">
        Using Google sign-in means:
    </p>
    <ul style="color: #666; font-size: 14px;">
        <li>No password to remember</li>
        <li>Enhanced security from Google</li>
        <li>Quick and easy access</li>
    </ul>

    <p style="color: #666; font-size: 14px;">
        If you didn't request this, you can safely ignore this email.
    </p>

    <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">

    <p style="color: #999; font-size: 12px; text-align: center;">
        © 2024 The Predictions. All rights reserved.
    </p>
</body>
</html>
```

### Plain Text Version

```
Hi {{ params.firstName }},

We received a request to reset your password, but your account is set up to sign in with Google.

You don't have a password to reset! Instead, please use the "Continue with Google" button on our login page to access your account.

Go to login page: {{ params.loginLink }}

Using Google sign-in means:
- No password to remember
- Enhanced security from Google
- Quick and easy access

If you didn't request this, you can safely ignore this email.

---
© 2024 The Predictions. All rights reserved.
```

---

## Configuration Steps

### Step 1: Create Templates in Brevo

1. Log in to your Brevo account
2. Navigate to **Campaigns** → **Templates** → **Email Templates**
3. Click **New Template**
4. Create both templates using the content above
5. Note the **Template ID** for each (shown in template URL or details)

### Step 2: Add Template IDs to Configuration

Add the template IDs to your configuration:

**For local development** (`appsettings.Development.json`):

```json
{
  "Brevo": {
    "ApiKey": "your-api-key",
    "SendFromName": "The Predictions",
    "SendFromEmail": "noreply@thepredictions.co.uk",
    "Templates": {
      "JoinLeagueRequest": 1,
      "PredictionsMissing": 2,
      "PasswordReset": 3,
      "PasswordResetGoogleUser": 4
    }
  }
}
```

**For production** (Azure Key Vault):

Add these secrets:
- `Brevo--Templates--PasswordReset` = `<template_id>`
- `Brevo--Templates--PasswordResetGoogleUser` = `<template_id>`

### Step 3: Update TemplateSettings Class

Ensure `TemplateSettings.cs` has the new properties (from Task 1):

```csharp
public class TemplateSettings
{
    public long JoinLeagueRequest { get; set; }
    public long PredictionsMissing { get; set; }
    public long PasswordReset { get; set; }
    public long PasswordResetGoogleUser { get; set; }
}
```

## Verification

- [ ] Password Reset template created in Brevo
- [ ] Google Sign-In Reminder template created in Brevo
- [ ] Both templates have correct parameters configured
- [ ] Template IDs added to development configuration
- [ ] Template IDs added to production configuration (Key Vault)
- [ ] Test email sends successfully with correct formatting
- [ ] Links in emails work correctly
- [ ] Emails render well on mobile devices

## Testing Tips

1. **Brevo Test Feature**: Use Brevo's "Send Test" feature to preview emails
2. **Parameter Testing**: Use test data like:
   ```json
   {
     "firstName": "Test User",
     "resetLink": "https://localhost:5001/authentication/reset-password?token=test&email=test@example.com"
   }
   ```
3. **Mobile Preview**: Check email rendering in Brevo's mobile preview mode

## Notes

- Template IDs are numeric (long) in Brevo
- The `{{ params.paramName }}` syntax is Brevo's templating language
- Always include a plain text version for email clients that don't render HTML
- The button colour `#4ade80` matches the app's green button colour
- The Google button uses Google's brand blue `#4285f4`
- Consider adding your logo URL to the templates
