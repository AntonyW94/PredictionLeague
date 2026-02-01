# Task 0: Manual Setup (Prerequisites)

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Complete all manual setup steps before implementing the password reset feature. This includes creating the database table, setting up email templates in Brevo, and updating configuration.

## Checklist

- [ ] Create database table
- [ ] Create Brevo email template: Password Reset
- [ ] Create Brevo email template: Google User Reminder
- [ ] Add template IDs to configuration
- [ ] Verify email sending works in development

---

## Step 1: Create Database Table

Run this SQL script against your database:

```sql
-- Create the PasswordResetTokens table
CREATE TABLE [PasswordResetTokens] (
    [Token] NVARCHAR(128) NOT NULL,
    [UserId] NVARCHAR(450) NOT NULL,
    [CreatedAtUtc] DATETIME2 NOT NULL,
    [ExpiresAtUtc] DATETIME2 NOT NULL,

    CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Token]),
    CONSTRAINT [FK_PasswordResetTokens_AspNetUsers]
        FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

-- Index for looking up tokens by user (rate limiting, cleanup)
CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens]([UserId]);

-- Index for cleanup of expired tokens
CREATE INDEX [IX_PasswordResetTokens_ExpiresAtUtc] ON [PasswordResetTokens]([ExpiresAtUtc]);
```

### Verify Table Creation

```sql
-- Check the table exists and has correct structure
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'PasswordResetTokens';
```

Expected output:
| COLUMN_NAME | DATA_TYPE | CHARACTER_MAXIMUM_LENGTH | IS_NULLABLE |
|-------------|-----------|--------------------------|-------------|
| Token | nvarchar | 128 | NO |
| UserId | nvarchar | 450 | NO |
| CreatedAtUtc | datetime2 | NULL | NO |
| ExpiresAtUtc | datetime2 | NULL | NO |

---

## Step 2: Create Brevo Email Templates

You need to create **two** email templates in Brevo (Sendinblue).

### Template 1: Password Reset Email

**Purpose:** Sent to users who have a password and request a reset.

**Template Name:** `Password Reset`

**Subject Line:** `Reset your password`

**Template Parameters:**
| Parameter | Description | Example |
|-----------|-------------|---------|
| `firstName` | User's first name | John |
| `resetLink` | Full URL to reset password | `https://thepredictions.co.uk/authentication/reset-password?token=abc123...` |

**Suggested Email Content:**

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Reset Your Password</title>
</head>
<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
        <h1 style="color: #4a154b;">Reset Your Password</h1>

        <p>Hi {{ params.firstName }},</p>

        <p>We received a request to reset your password for your Predictions account.</p>

        <p>Click the button below to set a new password:</p>

        <p style="text-align: center; margin: 30px 0;">
            <a href="{{ params.resetLink }}"
               style="background-color: #4a154b; color: white; padding: 12px 30px;
                      text-decoration: none; border-radius: 5px; display: inline-block;">
                Reset Password
            </a>
        </p>

        <p><strong>This link will expire in 1 hour.</strong></p>

        <p>If you didn't request this, you can safely ignore this email. Your password won't be changed.</p>

        <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">

        <p style="color: #666; font-size: 12px;">
            If the button doesn't work, copy and paste this link into your browser:<br>
            <a href="{{ params.resetLink }}" style="color: #4a154b;">{{ params.resetLink }}</a>
        </p>
    </div>
</body>
</html>
```

---

### Template 2: Google User Reminder Email

**Purpose:** Sent to users who signed up with Google and try to reset their password.

**Template Name:** `Password Reset - Google User`

**Subject Line:** `Sign in with Google`

**Template Parameters:**
| Parameter | Description | Example |
|-----------|-------------|---------|
| `firstName` | User's first name | John |
| `loginLink` | Link to the login page | `https://thepredictions.co.uk/authentication/login` |

**Suggested Email Content:**

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Sign in with Google</title>
</head>
<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
        <h1 style="color: #4a154b;">Sign in with Google</h1>

        <p>Hi {{ params.firstName }},</p>

        <p>We received a request to reset your password, but your account is set up to sign in with Google.</p>

        <p>You don't have a password to reset - instead, please sign in using the <strong>"Sign in with Google"</strong> button on our login page:</p>

        <p style="text-align: center; margin: 30px 0;">
            <a href="{{ params.loginLink }}"
               style="background-color: #4285f4; color: white; padding: 12px 30px;
                      text-decoration: none; border-radius: 5px; display: inline-block;">
                Go to Login Page
            </a>
        </p>

        <p>If you didn't request this, you can safely ignore this email.</p>

        <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">

        <p style="color: #666; font-size: 12px;">
            If you'd like to set up a password for your account in addition to Google sign-in,
            please contact us for assistance.
        </p>
    </div>
</body>
</html>
```

---

## Step 3: Note Your Template IDs

After creating each template in Brevo, note the **Template ID** (visible in the URL or template settings).

| Template | Template ID |
|----------|-------------|
| Password Reset | `________` |
| Password Reset - Google User | `________` |

---

## Step 4: Update Configuration

### Option A: Local Development (appsettings.Development.json)

```json
{
  "Brevo": {
    "ApiKey": "your-api-key",
    "SendFromName": "The Predictions",
    "SendFromEmail": "noreply@thepredictions.co.uk",
    "Templates": {
      "JoinLeagueRequest": 1,
      "PredictionsMissing": 2,
      "PasswordReset": YOUR_TEMPLATE_ID_HERE,
      "PasswordResetGoogleUser": YOUR_TEMPLATE_ID_HERE
    }
  }
}
```

### Option B: Production (Azure Key Vault)

Add these secrets to your Key Vault:

| Secret Name | Value |
|-------------|-------|
| `Brevo--Templates--PasswordReset` | Your template ID |
| `Brevo--Templates--PasswordResetGoogleUser` | Your template ID |

---

## Step 5: Verify Email Sending (Optional)

Before implementing the feature, you can test that email sending works by temporarily adding a test endpoint or using the existing email infrastructure.

### Quick Verification Query

Check that the templates are configured:

```csharp
// In a controller or test
var settings = _configuration.GetSection("Brevo:Templates").Get<TemplateSettings>();
Console.WriteLine($"PasswordReset: {settings?.PasswordReset}");
Console.WriteLine($"PasswordResetGoogleUser: {settings?.PasswordResetGoogleUser}");
```

Both should return non-zero values.

---

## Troubleshooting

### Table Creation Issues

**Error: "There is already an object named 'PasswordResetTokens'"**
- Table already exists. Check if it has the correct structure.

**Error: "Foreign key constraint failed"**
- Ensure `AspNetUsers` table exists first.

### Email Template Issues

**Emails not sending:**
1. Check Brevo API key is valid
2. Verify sender email is verified in Brevo
3. Check template IDs match configuration
4. Check Brevo logs for delivery issues

**Template variables not replaced:**
- Ensure you're using `{{ params.firstName }}` not `{{ firstName }}`
- Check parameter names match exactly (case-sensitive)

---

## Next Steps

Once all manual setup is complete, proceed to:
1. [Task 1: Domain & Infrastructure](./01-domain-infrastructure.md)
2. [Task 2: Request Password Reset Command](./02-request-password-reset-command.md)
3. Continue through remaining tasks...
