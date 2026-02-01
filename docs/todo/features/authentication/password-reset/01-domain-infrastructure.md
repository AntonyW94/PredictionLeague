# Task 1: Domain & Infrastructure

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add password reset methods to the `IUserManager` interface and `UserManagerService` implementation, and configure the token expiry time.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Services/IUserManager.cs` | Modify | Add password reset method signatures |
| `PredictionLeague.Infrastructure/Services/UserManagerService.cs` | Modify | Implement password reset methods |
| `PredictionLeague.Infrastructure/DependencyInjection.cs` | Modify | Configure token expiry to 1 hour |
| `PredictionLeague.Application/Configuration/TemplateSettings.cs` | Modify | Add new email template IDs |

## Implementation Steps

### Step 1: Add Methods to IUserManager Interface

Add the following methods to the `#region Read` and `#region Update` sections:

```csharp
// In IUserManager.cs

#region Read

// ... existing methods ...

Task<bool> HasPasswordAsync(ApplicationUser user);

#endregion

#region Update

// ... existing methods ...

Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user);
Task<UserManagerResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword);

#endregion
```

### Step 2: Implement Methods in UserManagerService

```csharp
// In UserManagerService.cs

#region Read

// ... existing methods ...

public async Task<bool> HasPasswordAsync(ApplicationUser user)
{
    return await _userManager.HasPasswordAsync(user);
}

#endregion

#region Update

// ... existing methods ...

public async Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user)
{
    return await _userManager.GeneratePasswordResetTokenAsync(user);
}

public async Task<UserManagerResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
{
    var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
    return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
}

#endregion
```

### Step 3: Configure Token Expiry

In `PredictionLeague.Infrastructure/DependencyInjection.cs`, add the token lifespan configuration after the Identity setup:

```csharp
// After services.AddIdentity<ApplicationUser, IdentityRole>(...) block

services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});
```

**Required using:**
```csharp
using Microsoft.AspNetCore.Identity;
```

### Step 4: Add Email Template IDs to TemplateSettings

```csharp
// In TemplateSettings.cs

public class TemplateSettings
{
    public long JoinLeagueRequest { get; set; }
    public long PredictionsMissing { get; set; }
    public long PasswordReset { get; set; }        // NEW
    public long PasswordResetGoogleUser { get; set; }  // NEW
}
```

## Code Patterns to Follow

The `UserManagerService` follows a consistent pattern of wrapping `UserManager<ApplicationUser>` methods and converting `IdentityResult` to `UserManagerResult`:

```csharp
// Pattern for methods returning IdentityResult
public async Task<UserManagerResult> SomeMethodAsync(...)
{
    var result = await _userManager.SomeMethodAsync(...);
    return result.Succeeded
        ? UserManagerResult.Success()
        : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
}

// Pattern for methods returning simple values
public async Task<bool> SomeCheckAsync(...)
{
    return await _userManager.SomeCheckAsync(...);
}
```

## Verification

- [ ] `IUserManager` interface compiles with new method signatures
- [ ] `UserManagerService` compiles with new implementations
- [ ] Solution builds without errors
- [ ] `DataProtectionTokenProviderOptions` configured with 1-hour lifespan
- [ ] `TemplateSettings` has two new properties for email template IDs

## Edge Cases to Consider

- `GeneratePasswordResetTokenAsync` should only be called for users who exist
- `ResetPasswordAsync` will fail with clear error if token is invalid/expired
- `HasPasswordAsync` returns `false` for Google-only users (no password hash set)

## Notes

- ASP.NET Identity's `GeneratePasswordResetTokenAsync` creates a token tied to the user's `SecurityStamp`
- The token is automatically invalidated when:
  - Password is changed (SecurityStamp updates)
  - Token expires (1 hour)
  - User's SecurityStamp is manually regenerated
- The token needs to be URL-encoded when used in email links
