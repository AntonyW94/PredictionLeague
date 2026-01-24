# Fix 01: IDOR - Unauthorized League Update

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P0 - Critical** - Fix immediately

## Severity
**Critical** - Any authenticated user can modify any league

## CWE Reference
[CWE-639: Authorization Bypass Through User-Controlled Key](https://cwe.mitre.org/data/definitions/639.html)

---

## Problem Description

The `UpdateLeagueAsync` endpoint and its handler do not verify that the requesting user is the league administrator before allowing modifications. Any authenticated user can update any league's name, price, and entry deadline by simply knowing the league ID.

### Affected Files

| File | Location |
|------|----------|
| Controller | `PredictionLeague.API/Controllers/LeaguesController.cs:201-216` |
| Command | `PredictionLeague.Application/Features/Leagues/Commands/UpdateLeagueCommand.cs` |
| Handler | `PredictionLeague.Application/Features/Leagues/Commands/UpdateLeagueCommandHandler.cs:19-41` |

### Current Vulnerable Code

**Controller (LeaguesController.cs:201-216)**
```csharp
[HttpPut("{leagueId:int}/update")]
public async Task<IActionResult> UpdateLeagueAsync(
    int leagueId,
    [FromBody] UpdateLeagueRequest request,
    CancellationToken cancellationToken)
{
    var command = new UpdateLeagueCommand(
        leagueId,
        request.Name,
        request.Price,
        request.EntryDeadlineUtc);
    // BUG: CurrentUserId not passed to command

    await _mediator.Send(command, cancellationToken);
    return NoContent();
}
```

**Handler (UpdateLeagueCommandHandler.cs:19-41)**
```csharp
public async Task Handle(UpdateLeagueCommand request, CancellationToken cancellationToken)
{
    var league = await _leagueRepository.GetByIdAsync(request.Id, cancellationToken);
    Guard.Against.EntityNotFound(request.Id, league, "League");

    // BUG: No authorization check - anyone can update!

    if (league.EntryDeadlineUtc < DateTime.UtcNow)
        throw new InvalidOperationException("Cannot update a league after the entry deadline has passed.");

    league.Update(request.Name, request.Price, request.EntryDeadlineUtc);
    await _leagueRepository.UpdateAsync(league, cancellationToken);
}
```

### Attack Scenario

1. Attacker registers an account and joins a league (or just enumerates league IDs)
2. Attacker sends PUT request to `/api/leagues/123/update` with modified values
3. League is updated without authorization check
4. League administrator's settings are overwritten

---

## Solution

### Step 1: Update the Command

Add `UserId` parameter to the command record.

**File**: `PredictionLeague.Application/Features/Leagues/Commands/UpdateLeagueCommand.cs`

```csharp
public record UpdateLeagueCommand(
    int Id,
    string Name,
    decimal Price,
    DateTime EntryDeadlineUtc,
    string UserId) : IRequest;
```

### Step 2: Update the Handler

Add authorization check before allowing update.

**File**: `PredictionLeague.Application/Features/Leagues/Commands/UpdateLeagueCommandHandler.cs`

```csharp
public async Task Handle(UpdateLeagueCommand request, CancellationToken cancellationToken)
{
    var league = await _leagueRepository.GetByIdAsync(request.Id, cancellationToken);
    Guard.Against.EntityNotFound(request.Id, league, "League");

    // ADD: Authorization check
    if (league.AdministratorUserId != request.UserId)
    {
        throw new UnauthorizedAccessException(
            "Only the league administrator can update the league.");
    }

    if (league.EntryDeadlineUtc < DateTime.UtcNow)
        throw new InvalidOperationException(
            "Cannot update a league after the entry deadline has passed.");

    league.Update(request.Name, request.Price, request.EntryDeadlineUtc);
    await _leagueRepository.UpdateAsync(league, cancellationToken);
}
```

### Step 3: Update the Controller

Pass `CurrentUserId` to the command.

**File**: `PredictionLeague.API/Controllers/LeaguesController.cs`

```csharp
[HttpPut("{leagueId:int}/update")]
public async Task<IActionResult> UpdateLeagueAsync(
    int leagueId,
    [FromBody] UpdateLeagueRequest request,
    CancellationToken cancellationToken)
{
    var command = new UpdateLeagueCommand(
        leagueId,
        request.Name,
        request.Price,
        request.EntryDeadlineUtc,
        CurrentUserId);  // ADD: Pass user ID

    await _mediator.Send(command, cancellationToken);
    return NoContent();
}
```

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.
> Test code is preserved below for future implementation.

### Manual Testing (Required for this implementation)

1. Log in as User A, create a league
2. Log in as User B, attempt to update User A's league via API
3. Verify request is rejected with appropriate error message

### Future: Unit Tests

<details>
<summary>Click to expand test code for future implementation</summary>

```csharp
[Fact]
public async Task Handle_WhenUserIsNotAdministrator_ThrowsUnauthorizedAccessException()
{
    // Arrange
    var league = CreateTestLeague(administratorUserId: "admin-user-id");
    var command = new UpdateLeagueCommand(1, "New Name", 10m, DateTime.UtcNow.AddDays(7), "other-user-id");

    _leagueRepositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
        .ReturnsAsync(league);

    // Act & Assert
    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => _handler.Handle(command, CancellationToken.None));
}

[Fact]
public async Task Handle_WhenUserIsAdministrator_UpdatesLeague()
{
    // Arrange
    var league = CreateTestLeague(administratorUserId: "admin-user-id");
    var command = new UpdateLeagueCommand(1, "New Name", 10m, DateTime.UtcNow.AddDays(7), "admin-user-id");

    _leagueRepositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
        .ReturnsAsync(league);

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    _leagueRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<League>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

</details>

### Future: Integration Tests

1. Authenticated user attempts to update their own league - should succeed
2. Authenticated user attempts to update another user's league - should return 401/403
3. Unauthenticated user attempts to update a league - should return 401

---

## Rollback Plan

If issues arise after deployment:
1. Revert the three file changes
2. Redeploy previous version
3. Monitor for unauthorized modifications while investigating

---

## Checklist

- [ ] Update `UpdateLeagueCommand` with `UserId` parameter
- [ ] Update `UpdateLeagueCommandHandler` with authorization check
- [ ] Update `LeaguesController` to pass `CurrentUserId`
- [ ] Manual testing complete
- [ ] Code review approved
- [ ] Deployed to production
- [ ] Verified in production

### Future (when test projects added)
- [ ] Write unit tests
- [ ] Write integration tests
