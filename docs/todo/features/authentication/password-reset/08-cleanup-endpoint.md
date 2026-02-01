# Task 8: Cleanup Scheduled Task

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a scheduled task endpoint for cleaning up expired data. Initially this will delete old password reset tokens, but the endpoint is designed to handle additional cleanup tasks in the future.

## Files to Create

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Admin/Tasks/Commands/CleanupExpiredDataCommand.cs` | Create | Command record |
| `PredictionLeague.Application/Features/Admin/Tasks/Commands/CleanupExpiredDataCommandHandler.cs` | Create | Command handler |

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Repositories/IPasswordResetTokenRepository.cs` | Modify | Add `DeleteTokensOlderThanAsync` method |
| `PredictionLeague.Infrastructure/Repositories/PasswordResetTokenRepository.cs` | Modify | Implement `DeleteTokensOlderThanAsync` |
| `PredictionLeague.API/Controllers/TasksController.cs` | Modify | Add cleanup endpoint |

## Implementation Steps

### Step 1: Add Repository Method

Add to `IPasswordResetTokenRepository.cs`:

```csharp
/// <summary>
/// Deletes all tokens created before the specified date (for scheduled cleanup).
/// </summary>
/// <returns>Number of tokens deleted.</returns>
Task<int> DeleteTokensOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);
```

Implement in `PasswordResetTokenRepository.cs`:

```csharp
public async Task<int> DeleteTokensOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
{
    const string sql = @"
        DELETE FROM [PasswordResetTokens]
        WHERE [CreatedAtUtc] < @OlderThanUtc";

    using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
    return await connection.ExecuteAsync(sql, new { OlderThanUtc = olderThanUtc });
}
```

### Step 2: Create the Command

```csharp
// CleanupExpiredDataCommand.cs

using MediatR;

namespace PredictionLeague.Application.Features.Admin.Tasks.Commands;

public record CleanupExpiredDataCommand : IRequest<CleanupResult>;

public record CleanupResult(
    int PasswordResetTokensDeleted
    // Add more properties as you add more cleanup tasks
);
```

### Step 3: Create the Command Handler

```csharp
// CleanupExpiredDataCommandHandler.cs

using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Tasks.Commands;

public class CleanupExpiredDataCommandHandler : IRequestHandler<CleanupExpiredDataCommand, CleanupResult>
{
    private const int PasswordResetTokenRetentionDays = 30;

    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly ILogger<CleanupExpiredDataCommandHandler> _logger;

    public CleanupExpiredDataCommandHandler(
        IPasswordResetTokenRepository passwordResetTokenRepository,
        ILogger<CleanupExpiredDataCommandHandler> logger)
    {
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _logger = logger;
    }

    public async Task<CleanupResult> Handle(CleanupExpiredDataCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scheduled cleanup task");

        // Clean up password reset tokens older than 30 days
        var tokenCutoffDate = DateTime.UtcNow.AddDays(-PasswordResetTokenRetentionDays);
        var tokensDeleted = await _passwordResetTokenRepository.DeleteTokensOlderThanAsync(
            tokenCutoffDate,
            cancellationToken);

        if (tokensDeleted > 0)
        {
            _logger.LogInformation(
                "Deleted {TokensDeleted} password reset tokens older than {CutoffDate:yyyy-MM-dd}",
                tokensDeleted,
                tokenCutoffDate);
        }

        // Add more cleanup tasks here as needed:
        // - Expired refresh tokens
        // - Old audit logs
        // - Orphaned data
        // etc.

        _logger.LogInformation("Scheduled cleanup task completed");

        return new CleanupResult(
            PasswordResetTokensDeleted: tokensDeleted
        );
    }
}
```

### Step 4: Add Endpoint to TasksController

```csharp
// In TasksController.cs

[HttpPost("cleanup")]
[SwaggerOperation(
    Summary = "Clean up expired data",
    Description = "Deletes expired and old data including password reset tokens older than 30 days. Called daily.")]
[SwaggerResponse(200, "Cleanup completed", typeof(CleanupResult))]
[SwaggerResponse(401, "Invalid or missing API key")]
public async Task<IActionResult> CleanupExpiredDataAsync(CancellationToken cancellationToken)
{
    var command = new CleanupExpiredDataCommand();
    var result = await _mediator.Send(command, cancellationToken);

    return Ok(result);
}
```

Add the required using statement:

```csharp
using PredictionLeague.Application.Features.Admin.Tasks.Commands;
```

### Step 5: Update Cron Job

Add a call to the cleanup endpoint in your daily cron job:

```bash
# Existing daily tasks
curl -X POST "https://api.thepredictions.co.uk/api/tasks/sync" -H "X-Api-Key: $API_KEY"
curl -X POST "https://api.thepredictions.co.uk/api/tasks/publish-upcoming-rounds" -H "X-Api-Key: $API_KEY"

# New cleanup task
curl -X POST "https://api.thepredictions.co.uk/api/tasks/cleanup" -H "X-Api-Key: $API_KEY"
```

Or if using a scheduled job service (e.g., Azure Logic Apps, GitHub Actions):

```yaml
# Example GitHub Actions step
- name: Run cleanup task
  run: |
    curl -X POST "${{ secrets.API_BASE_URL }}/api/tasks/cleanup" \
      -H "X-Api-Key: ${{ secrets.TASKS_API_KEY }}"
```

## Code Patterns to Follow

### Command Result Pattern

Return a result object so the caller can see what was cleaned up:

```csharp
public record CleanupResult(
    int PasswordResetTokensDeleted
    // Easy to extend with more properties
);
```

### Logging Pattern

Log the start, what was cleaned, and completion:

```csharp
_logger.LogInformation("Starting scheduled cleanup task");
_logger.LogInformation("Deleted {Count} items", count);
_logger.LogInformation("Scheduled cleanup task completed");
```

### Extensibility Pattern

The handler is designed to easily add more cleanup tasks:

```csharp
// Future additions:
var refreshTokensDeleted = await _refreshTokenRepository.DeleteExpiredAsync(...);
var auditLogsDeleted = await _auditLogRepository.DeleteOlderThanAsync(...);

return new CleanupResult(
    PasswordResetTokensDeleted: tokensDeleted,
    RefreshTokensDeleted: refreshTokensDeleted,  // New
    AuditLogsDeleted: auditLogsDeleted           // New
);
```

## Verification

- [ ] `DeleteTokensOlderThanAsync` method added to repository interface
- [ ] Repository implementation uses parameterised query
- [ ] Command and handler created in correct namespace
- [ ] Handler deletes tokens older than 30 days
- [ ] Handler logs deletion count
- [ ] Endpoint added to `TasksController`
- [ ] Endpoint returns `CleanupResult` with count
- [ ] Endpoint protected by `[ApiKeyAuthorise]` (inherited from controller)
- [ ] Cron job updated to call cleanup endpoint daily
- [ ] Solution compiles without errors

## Edge Cases to Consider

- **No tokens to delete** → Returns 0, logs nothing special
- **Large number of tokens** → Single DELETE is efficient, no pagination needed
- **Database connection fails** → Exception bubbles up, cron job sees error
- **Concurrent cleanup calls** → Safe - DELETE is idempotent

## Future Cleanup Tasks

This endpoint can be extended to clean up:

| Data | Retention | Notes |
|------|-----------|-------|
| Password reset tokens | 30 days | Already implemented |
| Expired refresh tokens | 30 days | Could add later |
| Old audit logs | 90 days | If audit logging is added |
| Failed login attempts | 7 days | If tracking is added |
| Orphaned data | N/A | Periodic integrity checks |

## Notes

- The 30-day retention is generous - tokens expire after 1 hour, so 30 days catches any edge cases
- Returning the count helps with monitoring (you could alert if suddenly 0 tokens deleted for weeks, suggesting the job isn't running)
- The endpoint returns 200 with a result body (not 204) so you can see what was cleaned up
- Using `DateTime.UtcNow` ensures consistent behaviour regardless of server timezone
