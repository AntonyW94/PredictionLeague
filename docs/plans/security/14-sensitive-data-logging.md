# Fix Plan: Sensitive Data Logging

> **SKIP FOR NOW**: This plan involves changes to token logging in the authentication system (RefreshTokenCommandHandler, ApiAuthenticationStateProvider). It has been deferred until the login system is ready to be modified.

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P1 - High |
| Severity | High |
| Type | Information Disclosure |
| CWE | CWE-532: Insertion of Sensitive Information into Log File |
| OWASP | A09:2021 Security Logging and Monitoring Failures |

---

## Vulnerabilities Addressed

### 1. Refresh Token Logging (HIGH)

**Files:**
- `PredictionLeague.Web.Client/Authentication/ApiAuthenticationStateProvider.cs:95`
- `PredictionLeague.Application/Features/Authentication/Commands/RefreshToken/RefreshTokenCommandHandler.cs:39,44`

**Current Code:**
```csharp
// ApiAuthenticationStateProvider.cs:95
_logger.LogInformation("Sending refresh token to API: {Token}", tokenModel.Token);

// RefreshTokenCommandHandler.cs:39
_logger.LogInformation("Corrected token by replacing spaces with '+': {CorrectedToken}", correctedToken);

// RefreshTokenCommandHandler.cs:44
_logger.LogWarning("GetByTokenAsync returned null or the token is not active. Token provided: {CorrectedToken}", correctedToken);
```

---

### 2. Email Address Logging (HIGH)

**Files:**
- `PredictionLeague.Application/Features/Authentication/Commands/RefreshToken/RefreshTokenCommandHandler.cs:55,61`
- `PredictionLeague.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs:80`

**Current Code:**
```csharp
// RefreshTokenCommandHandler.cs:55
_logger.LogInformation("Successfully found user: {Email}", user.Email);

// RefreshTokenCommandHandler.cs:61
_logger.LogInformation("Successfully generated new tokens for user: {Email}", user.Email);

// SendScheduledRemindersCommandHandler.cs:80
_logger.LogInformation("Sending Email Reminders: Sent {Email} Chase Notification for Round (ID: {RoundId})", user.Email, nextRound.Id);
```

---

## Fix Implementation

### Fix 1: Remove Token Logging

**File:** `PredictionLeague.Web.Client/Authentication/ApiAuthenticationStateProvider.cs`

**Change from:**
```csharp
_logger.LogInformation("Sending refresh token to API: {Token}", tokenModel.Token);
```

**To:**
```csharp
_logger.LogDebug("Sending refresh token request to API");
```

---

**File:** `PredictionLeague.Application/Features/Authentication/Commands/RefreshToken/RefreshTokenCommandHandler.cs`

**Change from:**
```csharp
// Line 39
_logger.LogInformation("Corrected token by replacing spaces with '+': {CorrectedToken}", correctedToken);

// Line 44
_logger.LogWarning("GetByTokenAsync returned null or the token is not active. Token provided: {CorrectedToken}", correctedToken);
```

**To:**
```csharp
// Line 39
_logger.LogDebug("Token format corrected (space replacement applied)");

// Line 44
_logger.LogWarning("Refresh token validation failed - token not found or inactive");
```

---

### Fix 2: Sanitise Email Logging

**File:** `PredictionLeague.Application/Features/Authentication/Commands/RefreshToken/RefreshTokenCommandHandler.cs`

**Change from:**
```csharp
// Line 55
_logger.LogInformation("Successfully found user: {Email}", user.Email);

// Line 61
_logger.LogInformation("Successfully generated new tokens for user: {Email}", user.Email);
```

**To:**
```csharp
// Line 55 - Use user ID instead of email
_logger.LogInformation("Successfully found user: {UserId}", user.Id);

// Line 61 - Use user ID instead of email
_logger.LogInformation("Successfully generated new tokens for user: {UserId}", user.Id);
```

---

**File:** `PredictionLeague.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs`

**Change from:**
```csharp
_logger.LogInformation("Sending Email Reminders: Sent {Email} Chase Notification for Round (ID: {RoundId})", user.Email, nextRound.Id);
```

**To:**
```csharp
_logger.LogInformation("Sent chase notification for Round {RoundId} to user {UserId}", nextRound.Id, user.UserId);
```

---

### Fix 3: Remove Partial Token Logging (AuthController)

**File:** `PredictionLeague.API/Controllers/AuthController.cs`

**If this line exists:**
```csharp
_logger.LogInformation("Processing refresh token from {TokenSource}. Token (first 10 chars): {TokenStart}",
    tokenSource, refreshToken.Length > 10 ? refreshToken[..10] : refreshToken);
```

**Change to:**
```csharp
_logger.LogInformation("Processing refresh token from {TokenSource}", tokenSource);
```

---

## Alternative: Structured Logging Sanitisation

If you need to keep some token information for debugging, use a hash:

```csharp
public static class LoggingExtensions
{
    public static string ToLogSafeHash(this string sensitiveValue)
    {
        if (string.IsNullOrEmpty(sensitiveValue))
            return "[empty]";

        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sensitiveValue));
        return Convert.ToHexString(hash)[..8];  // First 8 chars of hash
    }
}

// Usage:
_logger.LogWarning("Token validation failed for token hash: {TokenHash}", token.ToLogSafeHash());
```

---

## Testing

### Manual Test Steps

1. Trigger token refresh flow
2. Check application logs (Datadog/local)
3. Search for patterns:
   - Full refresh tokens (should NOT appear)
   - Email addresses (should NOT appear in auth logs)
   - User IDs (acceptable for tracing)

### Log Search Patterns (should return 0 results post-fix)

```
# Datadog query examples
"Sending refresh token to API"
"Corrected token by replacing spaces"
"Token provided:"
email:*@*.*  # In auth-related logs
```

---

## Configuration: Minimum Log Level

**File:** `appsettings.Production.json`

Ensure production logging doesn't include Debug level:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "PredictionLeague": "Information"
      }
    }
  }
}
```

This ensures:
- Debug logs with sensitive context removed don't appear in production
- Information logs only contain safe data (user IDs, not emails/tokens)

---

## Rollback Plan

These changes remove information from logs. If debugging is needed:
1. Temporarily enable Debug level logging in development only
2. Never re-add actual token/email values to production logs

---

## Notes

- User IDs are less sensitive than emails for correlation
- Token hashes allow correlation without exposing the actual token
- GDPR compliance requires PII (email) protection in logs
- Datadog retention policies should be reviewed for existing logs
