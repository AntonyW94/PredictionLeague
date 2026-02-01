# Task 1: Domain & Infrastructure

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create the `PasswordResetToken` entity and repository for storing password reset tokens in the database. Also add the `HasPasswordAsync` method to `IUserManager` for checking if users have a password set.

## Files to Create

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Domain/Models/PasswordResetToken.cs` | Create | Domain entity for reset tokens |
| `PredictionLeague.Application/Repositories/IPasswordResetTokenRepository.cs` | Create | Repository interface |
| `PredictionLeague.Infrastructure/Repositories/PasswordResetTokenRepository.cs` | Create | Repository implementation |

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Services/IUserManager.cs` | Modify | Add `HasPasswordAsync` method |
| `PredictionLeague.Infrastructure/Services/UserManagerService.cs` | Modify | Implement `HasPasswordAsync` |
| `PredictionLeague.Infrastructure/DependencyInjection.cs` | Modify | Register repository |
| `PredictionLeague.Application/Configuration/TemplateSettings.cs` | Modify | Add new email template IDs |

## Implementation Steps

### Step 1: Create the PasswordResetToken Entity

```csharp
// PasswordResetToken.cs

using System.Security.Cryptography;

namespace PredictionLeague.Domain.Models;

public class PasswordResetToken
{
    public string Token { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    // Private constructor for EF/Dapper
    private PasswordResetToken() { }

    // Public constructor for loading from database
    public PasswordResetToken(string token, string userId, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        Token = token;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>
    /// Factory method to create a new password reset token.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the reset.</param>
    /// <param name="expiryHours">How long the token should be valid (default 1 hour).</param>
    public static PasswordResetToken Create(string userId, int expiryHours = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var now = DateTime.UtcNow;

        return new PasswordResetToken
        {
            Token = token,
            UserId = userId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(expiryHours)
        };
    }

    /// <summary>
    /// Checks if the token has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc;
}
```

### Step 2: Create the Repository Interface

```csharp
// IPasswordResetTokenRepository.cs

using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IPasswordResetTokenRepository
{
    /// <summary>
    /// Stores a new password reset token.
    /// </summary>
    Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a token by its value. Returns null if not found.
    /// </summary>
    Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific token (after successful password reset).
    /// </summary>
    Task DeleteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all tokens for a user (when creating a new one or after successful reset).
    /// </summary>
    Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts tokens created for a user since the specified time (for rate limiting).
    /// </summary>
    Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all expired tokens (cleanup).
    /// </summary>
    Task DeleteExpiredTokensAsync(CancellationToken cancellationToken = default);
}
```

### Step 3: Create the Repository Implementation

```csharp
// PasswordResetTokenRepository.cs

using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PasswordResetTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO [PasswordResetTokens] ([Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc])
            VALUES (@Token, @UserId, @CreatedAtUtc, @ExpiresAtUtc)";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            token.Token,
            token.UserId,
            token.CreatedAtUtc,
            token.ExpiresAtUtc
        });
    }

    public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT [Token], [UserId], [CreatedAtUtc], [ExpiresAtUtc]
            FROM [PasswordResetTokens]
            WHERE [Token] = @Token";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PasswordResetToken>(sql, new { Token = token });
    }

    public async Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [Token] = @Token";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { Token = token });
    }

    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task<int> CountByUserIdSinceAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [PasswordResetTokens]
            WHERE [UserId] = @UserId AND [CreatedAtUtc] >= @SinceUtc";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, SinceUtc = sinceUtc });
    }

    public async Task DeleteExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM [PasswordResetTokens]
            WHERE [ExpiresAtUtc] < @NowUtc";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { NowUtc = DateTime.UtcNow });
    }
}
```

### Step 4: Create the Database Table

Run this SQL migration to create the table:

```sql
CREATE TABLE [PasswordResetTokens] (
    [Token] NVARCHAR(128) NOT NULL PRIMARY KEY,
    [UserId] NVARCHAR(450) NOT NULL,
    [CreatedAtUtc] DATETIME2 NOT NULL,
    [ExpiresAtUtc] DATETIME2 NOT NULL,

    CONSTRAINT [FK_PasswordResetTokens_AspNetUsers]
        FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

-- Index for looking up tokens by user (for rate limiting and cleanup)
CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens]([UserId]);

-- Index for cleanup of expired tokens
CREATE INDEX [IX_PasswordResetTokens_ExpiresAtUtc] ON [PasswordResetTokens]([ExpiresAtUtc]);
```

### Step 5: Add HasPasswordAsync to IUserManager Interface

```csharp
// In IUserManager.cs, add to #region Read

Task<bool> HasPasswordAsync(ApplicationUser user);
```

### Step 6: Implement HasPasswordAsync in UserManagerService

```csharp
// In UserManagerService.cs, add to #region Read

public async Task<bool> HasPasswordAsync(ApplicationUser user)
{
    return await _userManager.HasPasswordAsync(user);
}
```

### Step 7: Register Repository in DependencyInjection

```csharp
// In PredictionLeague.Infrastructure/DependencyInjection.cs
// Add with other repository registrations

services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
```

### Step 8: Add Email Template IDs to TemplateSettings

```csharp
// In TemplateSettings.cs

public class TemplateSettings
{
    public long JoinLeagueRequest { get; set; }
    public long PredictionsMissing { get; set; }
    public long PasswordReset { get; set; }           // NEW
    public long PasswordResetGoogleUser { get; set; } // NEW
}
```

## Code Patterns to Follow

### Entity Factory Method Pattern

Follow the existing domain entity pattern with factory methods:

```csharp
public static PasswordResetToken Create(string userId, int expiryHours = 1)
{
    // Validation
    ArgumentException.ThrowIfNullOrWhiteSpace(userId);

    // Create and return
    return new PasswordResetToken { ... };
}
```

### Repository Pattern

Follow the existing repository pattern using Dapper:

```csharp
public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
{
    const string sql = @"SELECT ... FROM [Table] WHERE [Id] = @Id";

    using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
    return await connection.QuerySingleOrDefaultAsync<T>(sql, new { Id = id });
}
```

### SQL Conventions

- Use square brackets around table and column names: `[PasswordResetTokens]`
- Use PascalCase for column names: `[CreatedAtUtc]`
- Use `@ParameterName` for parameters
- Use `DateTime.UtcNow` (never `DateTime.Now`)

## Verification

- [ ] `PasswordResetToken` entity has factory method and `IsExpired` property
- [ ] `IPasswordResetTokenRepository` interface defined with all required methods
- [ ] `PasswordResetTokenRepository` implements all methods using Dapper
- [ ] SQL uses parameterised queries (no string concatenation)
- [ ] Repository registered in DependencyInjection
- [ ] `IUserManager` has `HasPasswordAsync` method
- [ ] `UserManagerService` implements `HasPasswordAsync`
- [ ] `TemplateSettings` has two new email template ID properties
- [ ] Database migration script created
- [ ] Solution builds without errors

## Edge Cases to Consider

- **Token collision** → Extremely unlikely with 64 bytes of randomness (2^512 possibilities)
- **User deleted** → Foreign key CASCADE deletes their tokens automatically
- **Multiple reset requests** → Old tokens remain valid until expiry or used
- **Clock skew** → Use UTC everywhere to avoid timezone issues

## Notes

- Token is 64 bytes (512 bits) of cryptographically secure randomness
- Base64 URL-safe encoding makes it safe for URLs without additional encoding
- The `ON DELETE CASCADE` ensures tokens are cleaned up if a user is deleted
- Rate limiting is implemented by counting tokens created in the last hour
- Expired token cleanup happens opportunistically (no background job needed)
