# Fix Plan: Admin Command Validators

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P2 - Medium |
| Severity | Medium |
| Type | Input Validation |
| CWE | CWE-20: Improper Input Validation |
| OWASP | A03:2021 Injection |

---

## Vulnerabilities Addressed

### 1. UpdateMatchResultsCommand Missing Validator (MEDIUM)

**File:** `PredictionLeague.Application/Features/Admin/Rounds/Commands/UpdateMatchResultsCommand.cs`

**Issue:** No validation that match scores are within valid range (0-9).

**Current Command:**
```csharp
public record UpdateMatchResultsCommand(
    int RoundId,
    List<MatchResultDto> Matches) : IRequest, ITransactionalRequest;

public record MatchResultDto(int MatchId, int HomeScore, int AwayScore, MatchStatus Status);
```

---

### 2. UpdateUserRoleCommand Missing Validator (MEDIUM)

**File:** `PredictionLeague.API/Controllers/Admin/UsersController.cs:48`

**Issue:** Role parameter accepts arbitrary string without enum validation.

**Current Code:**
```csharp
[HttpPost("{userId}/role")]
public async Task<IActionResult> UpdateRoleAsync(
    string userId,
    [FromBody] string newRole,  // Any string accepted!
    CancellationToken cancellationToken)
{
    var command = new UpdateUserRoleCommand(userId, newRole);
    await _mediator.Send(command, cancellationToken);
    return NoContent();
}
```

---

## Fix Implementation

### Fix 1: Create UpdateMatchResultsRequestValidator

**File:** `PredictionLeague.Validators/Admin/Rounds/UpdateMatchResultsRequestValidator.cs` (NEW)

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Validators.Admin.Rounds;

public class UpdateMatchResultsRequestValidator : AbstractValidator<UpdateMatchResultsRequest>
{
    public UpdateMatchResultsRequestValidator()
    {
        RuleFor(x => x.RoundId)
            .GreaterThan(0)
            .WithMessage("Round ID must be greater than 0.");

        RuleFor(x => x.Matches)
            .NotEmpty()
            .WithMessage("At least one match result is required.");

        RuleForEach(x => x.Matches)
            .SetValidator(new MatchResultDtoValidator());
    }
}

public class MatchResultDtoValidator : AbstractValidator<MatchResultDto>
{
    public MatchResultDtoValidator()
    {
        RuleFor(x => x.MatchId)
            .GreaterThan(0)
            .WithMessage("Match ID must be greater than 0.");

        RuleFor(x => x.HomeScore)
            .InclusiveBetween(0, 9)
            .WithMessage("Home score must be between 0 and 9.");

        RuleFor(x => x.AwayScore)
            .InclusiveBetween(0, 9)
            .WithMessage("Away score must be between 0 and 9.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Match status must be a valid status value.");
    }
}
```

**Create Request DTO:**

**File:** `PredictionLeague.Contracts/Admin/Rounds/UpdateMatchResultsRequest.cs` (NEW)

```csharp
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Rounds;

public record UpdateMatchResultsRequest(
    int RoundId,
    List<MatchResultDto> Matches);

public record MatchResultDto(
    int MatchId,
    int HomeScore,
    int AwayScore,
    MatchStatus Status);
```

**Update Controller:**

**File:** `PredictionLeague.API/Controllers/Admin/RoundsController.cs`

```csharp
[HttpPut("{roundId:int}/results")]
public async Task<IActionResult> UpdateMatchResultsAsync(
    int roundId,
    [FromBody] UpdateMatchResultsRequest request,  // Use DTO instead of command
    CancellationToken cancellationToken)
{
    var command = new UpdateMatchResultsCommand(
        roundId,
        request.Matches.Select(m => new MatchResultDto(
            m.MatchId, m.HomeScore, m.AwayScore, m.Status)).ToList());

    await _mediator.Send(command, cancellationToken);
    return NoContent();
}
```

---

### Fix 2: Create UpdateUserRoleRequestValidator

**File:** `PredictionLeague.Validators/Admin/Users/UpdateUserRoleRequestValidator.cs` (NEW)

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Admin.Users;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Validators.Admin.Users;

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.NewRole)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Must(role => Enum.TryParse<ApplicationUserRole>(role, ignoreCase: true, out _))
            .WithMessage($"Role must be one of: {string.Join(", ", Enum.GetNames<ApplicationUserRole>())}");
    }
}
```

**Create Request DTO:**

**File:** `PredictionLeague.Contracts/Admin/Users/UpdateUserRoleRequest.cs` (NEW)

```csharp
namespace PredictionLeague.Contracts.Admin.Users;

public record UpdateUserRoleRequest(
    string UserId,
    string NewRole);
```

**Update Controller:**

**File:** `PredictionLeague.API/Controllers/Admin/UsersController.cs`

```csharp
[HttpPost("{userId}/role")]
public async Task<IActionResult> UpdateRoleAsync(
    string userId,
    [FromBody] UpdateUserRoleRequest request,  // Use DTO instead of raw string
    CancellationToken cancellationToken)
{
    var command = new UpdateUserRoleCommand(userId, request.NewRole);
    await _mediator.Send(command, cancellationToken);
    return NoContent();
}
```

---

## Register Validators

**File:** `PredictionLeague.Validators/DependencyInjection.cs`

Validators are registered automatically via assembly scanning:
```csharp
services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
```

Ensure the new validators are in the `PredictionLeague.Validators` assembly to be auto-registered.

---

## Testing

### UpdateMatchResults Validation Tests

**Valid Request:**
```json
POST /api/admin/rounds/1/results
{
    "roundId": 1,
    "matches": [
        { "matchId": 1, "homeScore": 2, "awayScore": 1, "status": "Completed" },
        { "matchId": 2, "homeScore": 0, "awayScore": 0, "status": "InProgress" }
    ]
}
```
Expected: 200 OK

**Invalid Request - Score out of range:**
```json
{
    "roundId": 1,
    "matches": [
        { "matchId": 1, "homeScore": 15, "awayScore": 99, "status": "Completed" }
    ]
}
```
Expected: 400 Bad Request with validation errors

### UpdateUserRole Validation Tests

**Valid Request:**
```json
POST /api/admin/users/abc123/role
{
    "userId": "abc123",
    "newRole": "Administrator"
}
```
Expected: 204 No Content

**Invalid Request - Bad role:**
```json
{
    "userId": "abc123",
    "newRole": "SuperAdmin"
}
```
Expected: 400 Bad Request - "Role must be one of: Administrator, Player"

---

## Rollback Plan

If validation causes issues:
1. Temporarily widen validation rules (e.g., allow 0-99 for scores)
2. Remove role whitelist check if legitimate roles are missing
3. Review and update validation rules based on actual business requirements

---

## Notes

- The score range 0-9 assumes standard football match scenarios
- For cup matches that go to penalties, consider if aggregate scores exceed 9
- Role validation prevents privilege escalation via arbitrary role injection
- All admin endpoints should continue to require Administrator role
