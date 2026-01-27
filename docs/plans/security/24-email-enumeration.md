# P1: User Enumeration via Registration

## Summary

**Severity:** P1 - High
**Type:** Information Disclosure / User Enumeration
**CWE:** CWE-204 (Observable Response Discrepancy)
**OWASP:** A07:2021 - Identification and Authentication Failures

## Description

The registration endpoint returns "User with this email already exists" when attempting to register with an existing email address. This allows attackers to enumerate valid user accounts.

## Affected Files

- `PredictionLeague.Application/Features/Authentication/Commands/Register/RegisterCommandHandler.cs` (line 24)

## Vulnerability Details

```csharp
public async Task<AuthenticationResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
{
    var userExists = await _userManager.FindByEmailAsync(request.Email);
    if (userExists != null)
        return new FailedAuthenticationResponse("User with this email already exists.");  // VULNERABLE

    // ... rest of registration
}
```

## Exploitation Scenario

1. Attacker creates a list of potential email addresses (common patterns, leaked databases)
2. Attacker sends registration requests for each email
3. Emails that return "User with this email already exists" are confirmed valid
4. Attacker now has a list of valid user accounts for:
   - Credential stuffing attacks
   - Phishing campaigns
   - Social engineering
   - Password spraying

## Attack Script Example

```python
emails = ["user1@example.com", "user2@example.com", ...]
valid_users = []

for email in emails:
    response = requests.post("/api/auth/register", json={
        "email": email,
        "password": "test123",
        "firstName": "Test",
        "lastName": "User"
    })
    if "already exists" in response.json().get("message", ""):
        valid_users.append(email)
```

## Recommended Fix

Return a generic message that doesn't reveal account existence:

```csharp
public async Task<AuthenticationResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
{
    var userExists = await _userManager.FindByEmailAsync(request.Email);
    if (userExists != null)
    {
        // Log the attempt for security monitoring
        _logger.LogWarning("Registration attempted for existing email: {Email}", request.Email);

        // Return generic message
        return new FailedAuthenticationResponse(
            "Registration could not be completed. If you already have an account, please try logging in.");
    }

    // ... rest of registration
}
```

### Alternative: Email Verification Flow

A more robust approach is to always "succeed" and send a verification email:

```csharp
if (userExists != null)
{
    // Send "account already exists" email to the user
    await _emailService.SendAccountExistsNotificationAsync(request.Email);

    // Return success message (same as new registration)
    return new SuccessfulRegistrationResponse(
        "Please check your email to verify your account.");
}
```

This way:
- Valid users get a "you already have an account" email
- Invalid registrations get a verification email
- Attacker can't distinguish between the two responses

## Testing

1. Register a new account with test@example.com
2. Attempt to register again with test@example.com
3. Verify response message is generic
4. Verify response time is similar to new registration (prevent timing attacks)

## Additional Considerations

- **Login endpoint**: Already uses generic message "Invalid email or password" (good)
- **Password reset**: Check that it also uses generic messages
- **Rate limiting**: Ensure rate limiting is active to slow enumeration attempts

## References

- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#authentication-and-error-messages)
- [CWE-204](https://cwe.mitre.org/data/definitions/204.html)
