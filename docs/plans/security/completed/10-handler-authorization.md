# Fix 10: Handler Authorization Improvements

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**Ongoing** - Fix as handlers are touched

## Severity
**High** - Authorization bypasses through direct handler invocation

## CWE Reference
[CWE-863: Incorrect Authorization](https://cwe.mitre.org/data/definitions/863.html)

---

## Problem Description

Many MediatR command handlers rely solely on controller-level `[Authorize]` attributes without verifying resource ownership or permissions within the handler itself. This creates vulnerabilities when:

1. Handlers are invoked from other services (not via HTTP)
2. Scheduled tasks bypass controller authorization
3. Future endpoints accidentally expose handlers without proper authorization

### Principle: Defense in Depth

Authorization should be enforced at **both** the controller level (authentication) and the handler level (authorization/ownership).

---

## Affected Handlers Summary

### Critical Priority (Fix Immediately)

| Handler | Issue | Impact |
|---------|-------|--------|
| `UpdateUserRoleCommandHandler` | No admin verification | Anyone can promote to admin |
| `SubmitPredictionsCommandHandler` | No user ownership check | Can submit predictions for other users |
| `UpdateUserDetailsCommandHandler` | No user ownership check | Can update other users' profiles |

### High Priority (Fix Soon)

| Handler | Issue | Impact |
|---------|-------|--------|
| `UpdateMatchResultsCommandHandler` | No admin verification | Non-admins could update match results |
| `CreateSeasonCommandHandler` | No admin verification | Non-admins could create seasons |
| `UpdateSeasonCommandHandler` | No admin verification | Non-admins could update seasons |
| `CreateRoundCommandHandler` | No admin verification | Non-admins could create rounds |
| `UpdateRoundCommandHandler` | No admin verification | Non-admins could update rounds |
| `CreateTeamCommandHandler` | No admin verification | Non-admins could create teams |
| `UpdateTeamCommandHandler` | No admin verification | Non-admins could update teams |
| `SyncSeasonWithApiCommandHandler` | No admin verification | Non-admins could trigger sync |
| `ProcessPrizesCommandHandler` | No admin verification | Non-admins could process prizes |

### Medium Priority (Fix When Touched)

| Handler | Issue | Impact |
|---------|-------|--------|
| `JoinLeagueCommandHandler` | Could join as another user | Limited impact |
| `LeaveLeagueCommandHandler` | Could leave as another user | Limited impact |

---

## Solution Patterns

### Pattern 1: User Self-Action Verification

For handlers where users act on their own resources:

```csharp
public class UpdateUserDetailsCommandHandler : IRequestHandler<UpdateUserDetailsCommand>
{
    public async Task Handle(UpdateUserDetailsCommand request, CancellationToken cancellationToken)
    {
        // VERIFY: User can only update their own details
        if (request.TargetUserId != request.CurrentUserId)
        {
            throw new UnauthorizedAccessException(
                "You can only update your own details.");
        }

        // ... rest of handler
    }
}
```

### Pattern 2: Resource Ownership Verification

For handlers where users manage resources they own:

```csharp
public class UpdateLeagueCommandHandler : IRequestHandler<UpdateLeagueCommand>
{
    public async Task Handle(UpdateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        // VERIFY: Only league administrator can update
        if (league.AdministratorUserId != request.CurrentUserId)
        {
            throw new UnauthorizedAccessException(
                "Only the league administrator can update the league.");
        }

        // ... rest of handler
    }
}
```

### Pattern 3: Admin Role Verification

For handlers that require admin privileges:

```csharp
public class UpdateMatchResultsCommandHandler : IRequestHandler<UpdateMatchResultsCommand>
{
    private readonly ICurrentUserService _currentUserService;

    public async Task Handle(UpdateMatchResultsCommand request, CancellationToken cancellationToken)
    {
        // VERIFY: User must be admin
        if (!await _currentUserService.IsAdminAsync(cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Only administrators can update match results.");
        }

        // ... rest of handler
    }
}
```

---

## Implementation: ICurrentUserService

Create a service to check user roles within handlers.

**File**: `PredictionLeague.Application/Common/Interfaces/ICurrentUserService.cs`

```csharp
namespace PredictionLeague.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    Task<bool> IsAdminAsync(CancellationToken cancellationToken = default);
    Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default);
}
```

**File**: `PredictionLeague.Infrastructure/Services/CurrentUserService.cs`

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Domain.Models;
using System.Security.Claims;

namespace PredictionLeague.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<bool> IsAdminAsync(CancellationToken cancellationToken = default)
    {
        return await IsInRoleAsync(ApplicationRoles.Administrator, cancellationToken);
    }

    public async Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(UserId))
            return false;

        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null)
            return false;

        return await _userManager.IsInRoleAsync(user, role);
    }
}
```

**Register in DI**:

```csharp
services.AddScoped<ICurrentUserService, CurrentUserService>();
```

---

## Detailed Fix: UpdateUserRoleCommandHandler

**File**: `PredictionLeague.Application/Features/Admin/Users/Commands/UpdateUserRoleCommandHandler.cs`

### Before (Vulnerable)

```csharp
public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateUserRoleCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        // NO AUTHORIZATION CHECK!
        var user = await _userManager.FindByIdAsync(request.UserId);
        // ... promotes/demotes user without verification
    }
}
```

### After (Fixed)

```csharp
public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUserService;

    public UpdateUserRoleCommandHandler(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _currentUserService = currentUserService;
    }

    public async Task Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        // ADD: Verify current user is admin
        if (!await _currentUserService.IsAdminAsync(cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Only administrators can modify user roles.");
        }

        // ADD: Prevent self-demotion (last admin protection)
        if (request.UserId == _currentUserService.UserId && !request.IsAdmin)
        {
            throw new InvalidOperationException(
                "You cannot remove your own administrator role.");
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, "User");

        // ... rest of handler
    }
}
```

---

## Detailed Fix: SubmitPredictionsCommandHandler

**File**: `PredictionLeague.Application/Features/Predictions/Commands/SubmitPredictionsCommandHandler.cs`

### Before (Vulnerable)

```csharp
public async Task Handle(SubmitPredictionsCommand request, CancellationToken cancellationToken)
{
    // Accepts UserId from request without verification
    foreach (var prediction in request.Predictions)
    {
        await _predictionRepository.UpsertAsync(
            request.UserId,  // Could be any user!
            prediction.MatchId,
            prediction.HomeScore,
            prediction.AwayScore,
            cancellationToken);
    }
}
```

### After (Fixed)

```csharp
public async Task Handle(SubmitPredictionsCommand request, CancellationToken cancellationToken)
{
    // ADD: Verify user is submitting their own predictions
    if (request.UserId != request.CurrentUserId)
    {
        throw new UnauthorizedAccessException(
            "You can only submit predictions for yourself.");
    }

    // ADD: Verify user is a member of the league
    var isMember = await _membershipService.IsApprovedMemberAsync(
        request.LeagueId,
        request.UserId,
        cancellationToken);

    if (!isMember)
    {
        throw new UnauthorizedAccessException(
            "You must be a member of this league to submit predictions.");
    }

    foreach (var prediction in request.Predictions)
    {
        await _predictionRepository.UpsertAsync(
            request.UserId,
            prediction.MatchId,
            prediction.HomeScore,
            prediction.AwayScore,
            cancellationToken);
    }
}
```

---

## Detailed Fix: Admin Handlers

All admin handlers should follow this pattern:

```csharp
public class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand, int>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateSeasonCommandHandler(
        ISeasonRepository seasonRepository,
        ICurrentUserService currentUserService)
    {
        _seasonRepository = seasonRepository;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        // ADD: Verify admin access
        if (!await _currentUserService.IsAdminAsync(cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Only administrators can create seasons.");
        }

        // Existing logic...
        var season = Season.Create(request.Name, request.StartDateUtc, request.EndDateUtc);
        var created = await _seasonRepository.CreateAsync(season, cancellationToken);
        return created.Id;
    }
}
```

Apply to:
- `CreateSeasonCommandHandler`
- `UpdateSeasonCommandHandler`
- `CreateRoundCommandHandler`
- `UpdateRoundCommandHandler`
- `UpdateMatchResultsCommandHandler`
- `CreateTeamCommandHandler`
- `UpdateTeamCommandHandler`
- `SyncSeasonWithApiCommandHandler`
- `ProcessPrizesCommandHandler`

---

## Alternative: MediatR Pipeline Behavior

For consistent authorization across all admin handlers, create a pipeline behavior:

**File**: `PredictionLeague.Application/Common/Behaviors/AdminAuthorizationBehavior.cs`

```csharp
using MediatR;

namespace PredictionLeague.Application.Common.Behaviors;

/// <summary>
/// Marker interface for commands that require admin authorization.
/// </summary>
public interface IAdminCommand { }

public class AdminAuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAdminCommand
{
    private readonly ICurrentUserService _currentUserService;

    public AdminAuthorizationBehavior(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!await _currentUserService.IsAdminAsync(cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "This operation requires administrator privileges.");
        }

        return await next();
    }
}
```

Then mark admin commands:

```csharp
public record CreateSeasonCommand(string Name, DateTime StartDateUtc, DateTime EndDateUtc)
    : IRequest<int>, IAdminCommand;  // Add IAdminCommand marker
```

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.
> Test code is preserved below for future implementation.

### Manual Testing (Required for this implementation)

For each handler fix:
1. Log in as non-admin user, attempt admin operation - should return 401/403
2. Log in as admin user, attempt admin operation - should succeed
3. Attempt to modify resources belonging to another user - should return 401/403

### Future: Unit Tests for Each Handler

<details>
<summary>Click to expand test code for future implementation</summary>

```csharp
[Fact]
public async Task UpdateUserRole_WhenNotAdmin_ThrowsUnauthorizedAccessException()
{
    // Arrange
    _currentUserServiceMock.Setup(s => s.IsAdminAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var command = new UpdateUserRoleCommand("user-id", isAdmin: true);

    // Act & Assert
    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => _handler.Handle(command, CancellationToken.None));
}

[Fact]
public async Task UpdateUserRole_WhenAdmin_Succeeds()
{
    // Arrange
    _currentUserServiceMock.Setup(s => s.IsAdminAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var user = new ApplicationUser { Id = "user-id" };
    _userManagerMock.Setup(m => m.FindByIdAsync("user-id"))
        .ReturnsAsync(user);

    var command = new UpdateUserRoleCommand("user-id", isAdmin: true);

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    _userManagerMock.Verify(m => m.AddToRoleAsync(user, ApplicationRoles.Administrator), Times.Once);
}
```

</details>

---

## Files to Update Summary

| Handler | File | Fix Type |
|---------|------|----------|
| UpdateUserRoleCommandHandler | Features/Admin/Users/Commands/ | Admin check |
| SubmitPredictionsCommandHandler | Features/Predictions/Commands/ | Self + membership |
| UpdateUserDetailsCommandHandler | Features/Account/Commands/ | Self check |
| CreateSeasonCommandHandler | Features/Admin/Seasons/Commands/ | Admin check |
| UpdateSeasonCommandHandler | Features/Admin/Seasons/Commands/ | Admin check |
| CreateRoundCommandHandler | Features/Admin/Rounds/Commands/ | Admin check |
| UpdateRoundCommandHandler | Features/Admin/Rounds/Commands/ | Admin check |
| UpdateMatchResultsCommandHandler | Features/Admin/Rounds/Commands/ | Admin check |
| CreateTeamCommandHandler | Features/Admin/Teams/Commands/ | Admin check |
| UpdateTeamCommandHandler | Features/Admin/Teams/Commands/ | Admin check |
| SyncSeasonWithApiCommandHandler | Features/Admin/Seasons/Commands/ | Admin check |
| ProcessPrizesCommandHandler | Features/Admin/Rounds/Commands/ | Admin check |
| JoinLeagueCommandHandler | Features/Leagues/Commands/ | Self check |
| LeaveLeagueCommandHandler | Features/Leagues/Commands/ | Self check |

---

## Checklist

### Infrastructure
- [ ] Create `ICurrentUserService` interface
- [ ] Create `CurrentUserService` implementation
- [ ] Register service in DI container
- [ ] (Optional) Create `IAdminCommand` marker interface
- [ ] (Optional) Create `AdminAuthorizationBehavior`

### Critical Handlers
- [ ] Fix `UpdateUserRoleCommandHandler`
- [ ] Fix `SubmitPredictionsCommandHandler`
- [ ] Fix `UpdateUserDetailsCommandHandler`

### Admin Handlers
- [ ] Fix `UpdateMatchResultsCommandHandler`
- [ ] Fix `CreateSeasonCommandHandler`
- [ ] Fix `UpdateSeasonCommandHandler`
- [ ] Fix `CreateRoundCommandHandler`
- [ ] Fix `UpdateRoundCommandHandler`
- [ ] Fix `CreateTeamCommandHandler`
- [ ] Fix `UpdateTeamCommandHandler`
- [ ] Fix `SyncSeasonWithApiCommandHandler`
- [ ] Fix `ProcessPrizesCommandHandler`

### Medium Priority Handlers
- [ ] Fix `JoinLeagueCommandHandler`
- [ ] Fix `LeaveLeagueCommandHandler`

### Testing
- [ ] Manual testing complete
- [ ] Code review approved

### Future (when test projects added)
- [ ] Write unit tests for authorization checks
- [ ] Write integration tests
