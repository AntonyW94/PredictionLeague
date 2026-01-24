# Fix 05: IDOR - League Members Access

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P1 - High** - Fix this sprint

## Severity
**High** - Any authenticated user can view member lists of any league

## CWE Reference
[CWE-639: Authorization Bypass Through User-Controlled Key](https://cwe.mitre.org/data/definitions/639.html)

---

## Problem Description

The `FetchLeagueMembersQuery` handler accepts a `CurrentUserId` parameter but never uses it to verify the requesting user is a member of the league. Any authenticated user can enumerate members of any league by simply providing different league IDs.

### Affected Files

| File | Lines |
|------|-------|
| `PredictionLeague.Application/Features/Leagues/Queries/FetchLeagueMembersQueryHandler.cs` | 18-64 |

### Current Vulnerable Code

```csharp
public async Task<LeagueMembersPageDto?> Handle(
    FetchLeagueMembersQuery request,
    CancellationToken cancellationToken)
{
    // CurrentUserId is passed but NEVER USED for authorization!
    const string sql = @"
        SELECT
            ...
        FROM [LeagueMembers] lm
        INNER JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
        INNER JOIN [AspNetUsers] u ON lm.[UserId] = u.[Id]
        WHERE l.[Id] = @LeagueId  -- No user verification!
        ORDER BY FullName;";

    var queryResult = await _dbConnection.QueryAsync<MemberQueryResult>(
        sql,
        cancellationToken,
        new { request.LeagueId, request.CurrentUserId, Pending = nameof(LeagueMemberStatus.Pending) }
        // CurrentUserId is passed to query but not used in WHERE clause!
    );
    // ...
}
```

### Attack Scenario

1. Attacker creates an account
2. Attacker calls `/api/leagues/1/members`, `/api/leagues/2/members`, etc.
3. Attacker harvests member names, emails, and membership status for all leagues
4. Information can be used for social engineering or competitive intelligence

---

## Solution

### Step 1: Add Membership Verification

Add a check to verify the requesting user is an approved member of the league before returning member data.

**File**: `PredictionLeague.Application/Features/Leagues/Queries/FetchLeagueMembersQueryHandler.cs`

```csharp
public async Task<LeagueMembersPageDto?> Handle(
    FetchLeagueMembersQuery request,
    CancellationToken cancellationToken)
{
    // STEP 1: Verify user is a member of the league
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
            "You must be a member of this league to view its members.");
    }

    // STEP 2: Now fetch the member list (existing logic)
    const string sql = @"
        SELECT
            lm.[UserId],
            u.[FirstName] + ' ' + u.[LastName] AS FullName,
            lm.[Status],
            l.[AdministratorUserId],
            CASE WHEN lm.[Status] = @Pending THEN 1 ELSE 0 END AS IsPending
        FROM [LeagueMembers] lm
        INNER JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
        INNER JOIN [AspNetUsers] u ON lm.[UserId] = u.[Id]
        WHERE l.[Id] = @LeagueId
        ORDER BY FullName;";

    var queryResult = await _dbConnection.QueryAsync<MemberQueryResult>(
        sql,
        cancellationToken,
        new { request.LeagueId, Pending = nameof(LeagueMemberStatus.Pending) });

    // ... rest of existing mapping logic
}
```

### Alternative: Allow League Administrator Access

If the league administrator should be able to view members even if not "approved" (edge case), modify the check:

```csharp
const string accessCheckSql = @"
    SELECT COUNT(*)
    FROM [Leagues] l
    LEFT JOIN [LeagueMembers] lm
        ON lm.[LeagueId] = l.[Id]
        AND lm.[UserId] = @CurrentUserId
        AND lm.[Status] = @ApprovedStatus
    WHERE l.[Id] = @LeagueId
      AND (l.[AdministratorUserId] = @CurrentUserId OR lm.[Id] IS NOT NULL);";

var hasAccess = await _dbConnection.QuerySingleOrDefaultAsync<int>(
    accessCheckSql,
    cancellationToken,
    new
    {
        request.LeagueId,
        request.CurrentUserId,
        ApprovedStatus = nameof(LeagueMemberStatus.Approved)
    });

if (hasAccess == 0)
{
    throw new UnauthorizedAccessException(
        "You must be a member of this league to view its members.");
}
```

---

## Testing Requirements

### Unit Tests

```csharp
[Fact]
public async Task Handle_WhenUserIsNotMember_ThrowsUnauthorizedAccessException()
{
    // Arrange
    _dbConnectionMock.Setup(d => d.QuerySingleOrDefaultAsync<int>(
        It.IsAny<string>(),
        It.IsAny<CancellationToken>(),
        It.IsAny<object>()))
        .ReturnsAsync(0);  // User is not a member

    var query = new FetchLeagueMembersQuery(LeagueId: 1, CurrentUserId: "non-member-id");

    // Act & Assert
    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => _handler.Handle(query, CancellationToken.None));
}

[Fact]
public async Task Handle_WhenUserIsMember_ReturnsMemberList()
{
    // Arrange
    _dbConnectionMock.Setup(d => d.QuerySingleOrDefaultAsync<int>(
        It.IsAny<string>(),
        It.IsAny<CancellationToken>(),
        It.IsAny<object>()))
        .ReturnsAsync(1);  // User is a member

    _dbConnectionMock.Setup(d => d.QueryAsync<MemberQueryResult>(
        It.IsAny<string>(),
        It.IsAny<CancellationToken>(),
        It.IsAny<object>()))
        .ReturnsAsync(new List<MemberQueryResult> { /* test data */ });

    var query = new FetchLeagueMembersQuery(LeagueId: 1, CurrentUserId: "member-id");

    // Act
    var result = await _handler.Handle(query, CancellationToken.None);

    // Assert
    Assert.NotNull(result);
}
```

### Integration Tests

1. User A creates a league
2. User B (not a member) attempts to fetch league members - should return 401/403
3. User B joins and is approved
4. User B fetches league members - should succeed

### Manual Testing

1. Log in as User A, create a league
2. Log in as User B (not a member)
3. Call API: `GET /api/leagues/{leagueId}/members`
4. Verify 401/403 response
5. Join User B to league, approve membership
6. Retry API call - should now succeed

---

## Checklist

- [ ] Add membership verification query at start of handler
- [ ] Throw `UnauthorizedAccessException` when user is not a member
- [ ] Consider allowing administrator access
- [ ] Write unit tests for authorization check
- [ ] Write integration tests
- [ ] Manual testing complete
- [ ] Code review approved
- [ ] Deployed to production
- [ ] Verified in production
