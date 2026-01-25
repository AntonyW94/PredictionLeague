# Fix Plan: IDOR - League Data Endpoints

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P1 - High |
| Severity | High |
| Type | Insecure Direct Object Reference (IDOR) |
| CWE | CWE-639: Authorization Bypass Through User-Controlled Key |
| OWASP | A01:2021 Broken Access Control |

---

## Vulnerabilities Addressed

Five endpoints allow any authenticated user to access league data without membership verification:

| # | Endpoint | Data Exposed |
|---|----------|--------------|
| 1 | `GET /api/leagues/{id}` | Entry code, league details |
| 2 | `GET /api/leagues/{id}/prizes` | Prize structure, amounts |
| 3 | `GET /api/leagues/{id}/winnings` | Winners, payout details |
| 4 | `GET /api/leagues/{id}/rounds-for-dashboard` | Round schedules, deadlines |
| 5 | `GET /api/leagues/{id}/months` | Monthly progress data |

---

## Fix Implementation

### Pattern to Apply

Use the existing `ILeagueMembershipService.EnsureApprovedMemberAsync()` pattern already implemented for leaderboard endpoints.

---

### Fix 1: GetLeagueByIdQuery

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetLeagueByIdQuery.cs`

**Add CurrentUserId parameter:**
```csharp
public record GetLeagueByIdQuery(int LeagueId, string CurrentUserId) : IRequest<LeagueDto?>;
```

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetLeagueByIdQueryHandler.cs`

**Add membership check:**
```csharp
public class GetLeagueByIdQueryHandler : IRequestHandler<GetLeagueByIdQuery, LeagueDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;
    private readonly ILeagueMembershipService _membershipService;

    public GetLeagueByIdQueryHandler(
        IApplicationReadDbConnection dbConnection,
        ILeagueMembershipService membershipService)
    {
        _dbConnection = dbConnection;
        _membershipService = membershipService;
    }

    public async Task<LeagueDto?> Handle(GetLeagueByIdQuery request, CancellationToken cancellationToken)
    {
        // ADD: Verify user is approved member
        await _membershipService.EnsureApprovedMemberAsync(
            request.LeagueId,
            request.CurrentUserId,
            cancellationToken);

        // Existing query logic...
    }
}
```

**File:** `PredictionLeague.API/Controllers/LeaguesController.cs`

**Update controller to pass CurrentUserId:**
```csharp
[HttpGet("{leagueId:int}")]
public async Task<ActionResult<LeagueDto>> GetLeagueByIdAsync(int leagueId, CancellationToken cancellationToken)
{
    var query = new GetLeagueByIdQuery(leagueId, CurrentUserId);  // Added CurrentUserId
    var result = await _mediator.Send(query, cancellationToken);

    if (result is null)
        return NotFound();

    return Ok(result);
}
```

---

### Fix 2: GetLeaguePrizesPageQuery

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetLeaguePrizesPageQuery.cs`

```csharp
public record GetLeaguePrizesPageQuery(int LeagueId, string CurrentUserId) : IRequest<LeaguePrizesPageDto?>;
```

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetLeaguePrizesPageQueryHandler.cs`

```csharp
public async Task<LeaguePrizesPageDto?> Handle(GetLeaguePrizesPageQuery request, CancellationToken cancellationToken)
{
    // ADD: Verify user is approved member
    await _membershipService.EnsureApprovedMemberAsync(
        request.LeagueId,
        request.CurrentUserId,
        cancellationToken);

    // Existing query logic...
}
```

---

### Fix 3: GetWinningsQuery

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetWinningsQuery.cs`

```csharp
public record GetWinningsQuery(int LeagueId, string CurrentUserId) : IRequest<WinningsDto?>;
```

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetWinningsQueryHandler.cs`

```csharp
public async Task<WinningsDto?> Handle(GetWinningsQuery request, CancellationToken cancellationToken)
{
    // ADD: Verify user is approved member
    await _membershipService.EnsureApprovedMemberAsync(
        request.LeagueId,
        request.CurrentUserId,
        cancellationToken);

    // Existing query logic...
}
```

---

### Fix 4: GetLeagueRoundsForDashboardQuery

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetLeagueRoundsForDashboardQuery.cs`

```csharp
public record GetLeagueRoundsForDashboardQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<RoundDto>>;
```

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetLeagueRoundsForDashboardQueryHandler.cs`

```csharp
public async Task<IEnumerable<RoundDto>> Handle(GetLeagueRoundsForDashboardQuery request, CancellationToken cancellationToken)
{
    // ADD: Verify user is approved member
    await _membershipService.EnsureApprovedMemberAsync(
        request.LeagueId,
        request.CurrentUserId,
        cancellationToken);

    // Existing query logic...
}
```

---

### Fix 5: GetMonthsForLeagueQuery

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetMonthsForLeagueQuery.cs`

```csharp
public record GetMonthsForLeagueQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<MonthDto>>;
```

**File:** `PredictionLeague.Application/Features/Leagues/Queries/GetMonthsForLeagueQueryHandler.cs`

```csharp
public async Task<IEnumerable<MonthDto>> Handle(GetMonthsForLeagueQuery request, CancellationToken cancellationToken)
{
    // ADD: Verify user is approved member
    await _membershipService.EnsureApprovedMemberAsync(
        request.LeagueId,
        request.CurrentUserId,
        cancellationToken);

    // Existing query logic...
}
```

---

### Controller Updates Summary

**File:** `PredictionLeague.API/Controllers/LeaguesController.cs`

Update all five endpoint methods to pass `CurrentUserId`:

```csharp
// 1. GetLeagueByIdAsync
var query = new GetLeagueByIdQuery(leagueId, CurrentUserId);

// 2. GetLeaguePrizesPageAsync
var query = new GetLeaguePrizesPageQuery(leagueId, CurrentUserId);

// 3. GetWinningsAsync
var query = new GetWinningsQuery(leagueId, CurrentUserId);

// 4. GetLeagueRoundsForDashboardAsync
var query = new GetLeagueRoundsForDashboardQuery(leagueId, CurrentUserId);

// 5. GetMonthsForLeagueAsync
var query = new GetMonthsForLeagueQuery(leagueId, CurrentUserId);
```

---

## Alternative: Admin Override

If admins should be able to view any league's data:

```csharp
public async Task<LeagueDto?> Handle(GetLeagueByIdQuery request, CancellationToken cancellationToken)
{
    // Allow if admin OR approved member
    if (!request.IsAdmin)
    {
        await _membershipService.EnsureApprovedMemberAsync(
            request.LeagueId,
            request.CurrentUserId,
            cancellationToken);
    }

    // Query logic...
}
```

---

## Testing

### Manual Test Steps

1. Create two users: UserA (league member) and UserB (not a member)
2. Create a league with UserA as admin
3. Log in as UserB
4. Attempt to access each endpoint with the league ID:
   - `GET /api/leagues/{leagueId}` → Should return 401/403
   - `GET /api/leagues/{leagueId}/prizes` → Should return 401/403
   - `GET /api/leagues/{leagueId}/winnings` → Should return 401/403
   - `GET /api/leagues/{leagueId}/rounds-for-dashboard` → Should return 401/403
   - `GET /api/leagues/{leagueId}/months` → Should return 401/403
5. Log in as UserA and verify all endpoints return data successfully

### Expected Responses

**Non-member access:**
```json
{
    "message": "You must be an approved member of this league to access this resource."
}
```
HTTP Status: 401 Unauthorized

**Member access:**
Normal data response with HTTP 200

---

## Rollback Plan

If issues arise, remove the membership check from individual handlers but keep the query parameter changes for future implementation.

---

## Notes

- The `ILeagueMembershipService` already exists and is used for leaderboard endpoints
- This follows the established pattern in the codebase
- Consider caching membership status to reduce database queries
