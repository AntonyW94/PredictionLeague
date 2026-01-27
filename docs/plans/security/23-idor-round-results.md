# P1: IDOR in League Dashboard Round Results

## Summary

**Severity:** P1 - High
**Type:** Insecure Direct Object Reference (IDOR)
**CWE:** CWE-639 (Authorization Bypass Through User-Controlled Key)
**OWASP:** A01:2021 - Broken Access Control

## Description

The `GetLeagueDashboardRoundResultsQueryHandler` returns prediction results for any league/round combination without validating that the requesting user is an approved member of the league.

## Affected Files

- `PredictionLeague.Application/Features/Leagues/Queries/GetLeagueDashboardRoundResultsQueryHandler.cs`

## Vulnerability Details

```csharp
public async Task<IEnumerable<PredictionResultDto>?> Handle(
    GetLeagueDashboardRoundResultsQuery request,
    CancellationToken cancellationToken)
{
    // NO membership check here!
    // Query directly fetches data for any LeagueId

    const string sql = @"...WHERE lm.[LeagueId] = @LeagueId...";
    var parameters = new
    {
        request.LeagueId,  // User-controlled, not validated
        request.RoundId,
        request.CurrentUserId,
        Approved = nameof(LeagueMemberStatus.Approved)
    };
    // Returns data without verifying CurrentUserId is a member
}
```

## Exploitation Scenario

1. User A is authenticated and a member of League 1
2. User A discovers League 2 exists (via enumeration or knowledge)
3. User A requests: `GET /api/leagues/2/rounds/5/results`
4. API returns all predictions, scores, and rankings for League 2
5. User A gains competitive intelligence about a league they shouldn't access

## Data Exposed

- All player names in the league
- All predictions for each match (after deadline)
- Player rankings and total points
- Applied boosts

## Recommended Fix

Add membership validation at the start of the handler:

```csharp
public async Task<IEnumerable<PredictionResultDto>?> Handle(
    GetLeagueDashboardRoundResultsQuery request,
    CancellationToken cancellationToken)
{
    // Add this validation
    await _membershipService.EnsureApprovedMemberAsync(
        request.CurrentUserId,
        request.LeagueId,
        cancellationToken);

    // Rest of existing code...
}
```

Inject `ILeagueMembershipService` into the handler:

```csharp
private readonly IApplicationReadDbConnection _dbConnection;
private readonly ILeagueMembershipService _membershipService;

public GetLeagueDashboardRoundResultsQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService)
{
    _dbConnection = dbConnection;
    _membershipService = membershipService;
}
```

## Testing

1. Create two users: UserA (member of League1), UserB (member of League2)
2. As UserA, request League2's round results
3. Verify 403 Forbidden or 404 Not Found response
4. Verify UserA can still access League1's results

## Related Files to Check

Similar patterns should be verified in:
- `GetLeagueDashboardQueryHandler` (already has check)
- `GetLeagueByIdQueryHandler` (already has check)
- Other league-scoped queries

## References

- [OWASP IDOR Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Insecure_Direct_Object_Reference_Prevention_Cheat_Sheet.html)
