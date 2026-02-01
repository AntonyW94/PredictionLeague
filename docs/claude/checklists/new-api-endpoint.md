# Checklist: Adding a New API Endpoint

Use this checklist when adding a new API endpoint to the project.

## Step 1: Create the Command or Query

Follow the appropriate checklist:
- For write operations: [`new-command.md`](new-command.md)
- For read operations: [`new-query.md`](new-query.md)

## Step 2: Create the API Controller Method

**Location:** `PredictionLeague.API/Controllers/{Area}Controller.cs`

### For Commands (POST/PUT/DELETE)

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaguesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeaguesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new league
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LeagueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LeagueDto>> Create(
        [FromBody] CreateLeagueRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();

        var command = new CreateLeagueCommand(
            request.Name,
            request.SeasonId,
            userId);

        var result = await _mediator.Send(command, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// Updates the league name
    /// </summary>
    [HttpPut("{id:int}/name")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateName(
        int id,
        [FromBody] UpdateLeagueNameRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();

        var command = new UpdateLeagueNameCommand(id, request.Name, userId);

        await _mediator.Send(command, ct);

        return NoContent();
    }
}
```

### For Queries (GET)

```csharp
/// <summary>
/// Gets a league by ID
/// </summary>
[HttpGet("{id:int}")]
[ProducesResponseType(typeof(LeagueDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<LeagueDto>> GetById(int id, CancellationToken ct)
{
    var userId = User.GetUserId();

    var query = new GetLeagueByIdQuery(id, userId);

    var result = await _mediator.Send(query, ct);

    if (result is null)
    {
        return NotFound();
    }

    return Ok(result);
}

/// <summary>
/// Gets all leagues for the current user
/// </summary>
[HttpGet]
[ProducesResponseType(typeof(IEnumerable<LeagueSummaryDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<IEnumerable<LeagueSummaryDto>>> GetMyLeagues(CancellationToken ct)
{
    var userId = User.GetUserId();

    var query = new GetMyLeaguesQuery(userId);

    var result = await _mediator.Send(query, ct);

    return Ok(result);
}
```

### Controller Checklist

- [ ] Inherits from `ControllerBase`
- [ ] Has `[ApiController]` attribute
- [ ] Has `[Route("api/[controller]")]` attribute
- [ ] Has `[Authorize]` attribute (unless public endpoint)
- [ ] Constructor injects `IMediator` only
- [ ] All methods have XML documentation (`<summary>`)
- [ ] All methods have `[ProducesResponseType]` attributes
- [ ] All methods accept `CancellationToken ct`
- [ ] POST returns `CreatedAtAction` with 201
- [ ] PUT/DELETE returns `NoContent()` with 204
- [ ] GET returns `Ok(result)` with 200
- [ ] Not found returns `NotFound()` with 404

## Step 3: Create Request DTOs (if needed)

**Location:** `PredictionLeague.Contracts/Requests/{Area}/{RequestName}.cs`

```csharp
public record CreateLeagueRequest(
    string Name,
    int SeasonId);

public record UpdateLeagueNameRequest(
    string Name);
```

### Request DTO Checklist

- [ ] Use `record` type
- [ ] One file per request
- [ ] Located in `PredictionLeague.Contracts/Requests/`
- [ ] No validation attributes (use FluentValidation)
- [ ] UK English spelling

## Step 4: Create Request Validators (if needed)

**Location:** `PredictionLeague.Validators/Requests/{RequestName}Validator.cs`

```csharp
public class CreateLeagueRequestValidator : AbstractValidator<CreateLeagueRequest>
{
    public CreateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("League name is required")
            .MaximumLength(100).WithMessage("League name cannot exceed 100 characters");

        RuleFor(x => x.SeasonId)
            .GreaterThan(0).WithMessage("Please select a valid season");
    }
}
```

## Step 5: Update Swagger Documentation

Swagger documentation is auto-generated from:
- XML comments (`<summary>`, `<param>`, `<returns>`)
- `[ProducesResponseType]` attributes
- Request/Response DTO properties

Ensure all are complete.

## Step 6: Add Authorisation (if needed)

For endpoints requiring specific permissions:

```csharp
// Require specific role
[Authorize(Roles = "Admin")]
[HttpDelete("{id:int}")]
public async Task<IActionResult> Delete(int id, CancellationToken ct)

// Require specific policy
[Authorize(Policy = "LeagueAdmin")]
[HttpPut("{id:int}/settings")]
public async Task<IActionResult> UpdateSettings(...)
```

## HTTP Method Guidelines

| Operation | Method | Route | Returns |
|-----------|--------|-------|---------|
| Create | POST | `/api/leagues` | 201 Created |
| Get one | GET | `/api/leagues/{id}` | 200 OK |
| Get many | GET | `/api/leagues` | 200 OK |
| Update (full) | PUT | `/api/leagues/{id}` | 204 No Content |
| Update (partial) | PUT | `/api/leagues/{id}/name` | 204 No Content |
| Delete | DELETE | `/api/leagues/{id}` | 204 No Content |

## Error Handling

The global exception handler manages errors. Controllers should:

```csharp
// Let exceptions propagate (global handler catches them)
var result = await _mediator.Send(command, ct);

// Only handle expected "not found" cases explicitly
if (result is null)
{
    return NotFound();
}
```

**Do NOT** wrap in try-catch unless handling specific recoverable errors.

## Verification Checklist

- [ ] Command or Query created (see respective checklists)
- [ ] Controller method added with correct HTTP verb
- [ ] Route follows REST conventions
- [ ] XML documentation complete
- [ ] `[ProducesResponseType]` for all responses
- [ ] `CancellationToken` passed to all async calls
- [ ] User ID obtained via `User.GetUserId()`
- [ ] Request DTO created (if needed)
- [ ] Request validator created (if needed)
- [ ] Authorisation configured appropriately
- [ ] Swagger documentation renders correctly
