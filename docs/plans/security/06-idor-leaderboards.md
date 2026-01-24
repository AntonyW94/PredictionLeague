# Fix 06: IDOR - Leaderboard Access

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P1 - High** - Fix this sprint

## Severity
**High** - Any authenticated user can view leaderboards for any league

## CWE Reference
[CWE-639: Authorization Bypass Through User-Controlled Key](https://cwe.mitre.org/data/definitions/639.html)

---

## Problem Description

Three leaderboard query handlers do not verify that the requesting user is a member of the league before returning leaderboard data. Any authenticated user can access competitive standings for any league.

### Affected Files

| File | Handler |
|------|---------|
| `PredictionLeague.Application/Features/Leagues/Queries/GetOverallLeaderboardQueryHandler.cs` | Overall standings |
| `PredictionLeague.Application/Features/Leagues/Queries/GetMonthlyLeaderboardQueryHandler.cs` | Monthly standings |
| `PredictionLeague.Application/Features/Leagues/Queries/GetExactScoresLeaderboardQueryHandler.cs` | Exact scores standings |

### Current Vulnerable Code

All three handlers follow the same pattern:

```csharp
public async Task<IEnumerable<LeaderboardEntryDto>> Handle(
    GetOverallLeaderboardQuery request,
    CancellationToken cancellationToken)
{
    // NO AUTHORIZATION CHECK!
    const string sql = @"
        SELECT ...
        FROM [LeagueMembers] lm
        WHERE lm.[LeagueId] = @LeagueId
        AND lm.[Status] = @ApprovedStatus";

    return await _dbConnection.QueryAsync<LeaderboardEntryDto>(
        sql,
        cancellationToken,
        new { request.LeagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
}
```

### Attack Scenario

1. Attacker joins or observes any public league
2. Attacker discovers league IDs (sequential or via enumeration)
3. Attacker calls leaderboard endpoints for competitor leagues
4. Attacker gains competitive intelligence about other leagues' standings

---

## Solution

### Option A: Create Shared Authorization Helper (Recommended)

Since multiple handlers need the same check, create a reusable service.

**Step 1: Create Authorization Service**

**File**: `PredictionLeague.Application/Common/Interfaces/ILeagueMembershipService.cs`

```csharp
namespace PredictionLeague.Application.Common.Interfaces;

public interface ILeagueMembershipService
{
    Task<bool> IsApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken);
    Task EnsureApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken);
}
```

**File**: `PredictionLeague.Infrastructure/Services/LeagueMembershipService.cs`

```csharp
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Infrastructure.Services;

public class LeagueMembershipService : ILeagueMembershipService
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public LeagueMembershipService(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<bool> IsApprovedMemberAsync(
        int leagueId,
        string userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [LeagueMembers]
            WHERE [LeagueId] = @LeagueId
              AND [UserId] = @UserId
              AND [Status] = @ApprovedStatus;";

        var count = await _dbConnection.QuerySingleOrDefaultAsync<int>(
            sql,
            cancellationToken,
            new { LeagueId = leagueId, UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return count > 0;
    }

    public async Task EnsureApprovedMemberAsync(
        int leagueId,
        string userId,
        CancellationToken cancellationToken)
    {
        var isMember = await IsApprovedMemberAsync(leagueId, userId, cancellationToken);

        if (!isMember)
        {
            throw new UnauthorizedAccessException(
                "You must be a member of this league to access this resource.");
        }
    }
}
```

**Step 2: Register Service**

**File**: `PredictionLeague.Infrastructure/DependencyInjection.cs`

```csharp
services.AddScoped<ILeagueMembershipService, LeagueMembershipService>();
```

**Step 3: Update Queries to Include UserId**

Update each query to include `CurrentUserId`:

```csharp
// Before
public record GetOverallLeaderboardQuery(int LeagueId) : IRequest<IEnumerable<LeaderboardEntryDto>>;

// After
public record GetOverallLeaderboardQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<LeaderboardEntryDto>>;
```

**Step 4: Update Handlers**

**File**: `GetOverallLeaderboardQueryHandler.cs`

```csharp
public class GetOverallLeaderboardQueryHandler
    : IRequestHandler<GetOverallLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;
    private readonly ILeagueMembershipService _membershipService;

    public GetOverallLeaderboardQueryHandler(
        IApplicationReadDbConnection dbConnection,
        ILeagueMembershipService membershipService)
    {
        _dbConnection = dbConnection;
        _membershipService = membershipService;
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(
        GetOverallLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        // ADD: Authorization check
        await _membershipService.EnsureApprovedMemberAsync(
            request.LeagueId,
            request.CurrentUserId,
            cancellationToken);

        // Existing query logic...
        const string sql = @"...";

        return await _dbConnection.QueryAsync<LeaderboardEntryDto>(
            sql,
            cancellationToken,
            new { request.LeagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }
}
```

Apply the same pattern to:
- `GetMonthlyLeaderboardQueryHandler`
- `GetExactScoresLeaderboardQueryHandler`

**Step 5: Update Controllers**

**File**: `LeaguesController.cs`

```csharp
[HttpGet("{leagueId:int}/leaderboard")]
public async Task<IActionResult> GetOverallLeaderboard(int leagueId, CancellationToken cancellationToken)
{
    var query = new GetOverallLeaderboardQuery(leagueId, CurrentUserId);  // Add CurrentUserId
    var result = await _mediator.Send(query, cancellationToken);
    return Ok(result);
}
```

---

### Option B: Inline Check (If Not Creating Shared Service)

If you prefer not to create a shared service, add the check inline in each handler:

```csharp
public async Task<IEnumerable<LeaderboardEntryDto>> Handle(
    GetOverallLeaderboardQuery request,
    CancellationToken cancellationToken)
{
    // Authorization check
    const string memberCheckSql = @"
        SELECT COUNT(*)
        FROM [LeagueMembers]
        WHERE [LeagueId] = @LeagueId
          AND [UserId] = @CurrentUserId
          AND [Status] = @ApprovedStatus;";

    var isMember = await _dbConnection.QuerySingleOrDefaultAsync<int>(
        memberCheckSql,
        cancellationToken,
        new
        {
            request.LeagueId,
            request.CurrentUserId,
            ApprovedStatus = nameof(LeagueMemberStatus.Approved)
        });

    if (isMember == 0)
    {
        throw new UnauthorizedAccessException(
            "You must be a member of this league to view its leaderboard.");
    }

    // Existing query logic...
}
```

---

## Files to Update Summary

| File | Changes |
|------|---------|
| `GetOverallLeaderboardQuery.cs` | Add `CurrentUserId` parameter |
| `GetOverallLeaderboardQueryHandler.cs` | Add authorization check |
| `GetMonthlyLeaderboardQuery.cs` | Add `CurrentUserId` parameter |
| `GetMonthlyLeaderboardQueryHandler.cs` | Add authorization check |
| `GetExactScoresLeaderboardQuery.cs` | Add `CurrentUserId` parameter |
| `GetExactScoresLeaderboardQueryHandler.cs` | Add authorization check |
| `LeaguesController.cs` | Pass `CurrentUserId` to all leaderboard queries |
| `ILeagueMembershipService.cs` | New interface (if using Option A) |
| `LeagueMembershipService.cs` | New implementation (if using Option A) |
| `DependencyInjection.cs` | Register service (if using Option A) |

---

## Testing Requirements

### Unit Tests

```csharp
[Fact]
public async Task GetOverallLeaderboard_WhenUserIsNotMember_ThrowsUnauthorizedAccessException()
{
    // Arrange
    _membershipServiceMock.Setup(m => m.EnsureApprovedMemberAsync(
        It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new UnauthorizedAccessException());

    var query = new GetOverallLeaderboardQuery(LeagueId: 1, CurrentUserId: "non-member");

    // Act & Assert
    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => _handler.Handle(query, CancellationToken.None));
}

[Fact]
public async Task GetOverallLeaderboard_WhenUserIsMember_ReturnsLeaderboard()
{
    // Arrange
    _membershipServiceMock.Setup(m => m.EnsureApprovedMemberAsync(
        It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var query = new GetOverallLeaderboardQuery(LeagueId: 1, CurrentUserId: "member");

    // Act
    var result = await _handler.Handle(query, CancellationToken.None);

    // Assert
    Assert.NotNull(result);
}
```

### Integration Tests

1. User who is not a member calls leaderboard endpoint - should return 401/403
2. User who is a member calls leaderboard endpoint - should return leaderboard data
3. Test all three leaderboard endpoints

---

## Checklist

- [ ] Create `ILeagueMembershipService` interface (Option A)
- [ ] Create `LeagueMembershipService` implementation (Option A)
- [ ] Register service in DI container (Option A)
- [ ] Update `GetOverallLeaderboardQuery` with `CurrentUserId`
- [ ] Update `GetOverallLeaderboardQueryHandler` with auth check
- [ ] Update `GetMonthlyLeaderboardQuery` with `CurrentUserId`
- [ ] Update `GetMonthlyLeaderboardQueryHandler` with auth check
- [ ] Update `GetExactScoresLeaderboardQuery` with `CurrentUserId`
- [ ] Update `GetExactScoresLeaderboardQueryHandler` with auth check
- [ ] Update `LeaguesController` to pass `CurrentUserId`
- [ ] Write unit tests for each handler
- [ ] Write integration tests
- [ ] Code review approved
- [ ] Deployed to production
- [ ] Verified in production
