# API Project Guidelines

Rules specific to the REST API project. For solution-wide patterns, see the root [`CLAUDE.md`](../CLAUDE.md).

## Controller Organisation

```
/api/auth           → Authentication (login, register, refresh)
/api/account        → User profile
/api/dashboard      → Dashboard data
/api/leagues        → League CRUD and membership
/api/predictions    → Prediction submission
/api/rounds         → Round queries
/api/admin/rounds   → Admin round management
/api/admin/seasons  → Admin season management
/api/tasks          → Background job triggers (API key protected)
```

## Controller Patterns

### Standard Controller Structure

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // Most endpoints require auth
public class LeaguesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeaguesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<LeagueDto>> Create(
        CreateLeagueCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LeagueDto>> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLeagueByIdQuery(id), ct);
        return Ok(result);
    }
}
```

### Key Patterns

- Controllers are thin - delegate to MediatR handlers
- Always accept `CancellationToken` and pass it through
- Use `ActionResult<T>` for typed responses
- Use `CreatedAtAction` for POST responses

## Authentication

### Token Types

| Type | Storage | Expiry | Use |
|------|---------|--------|-----|
| JWT Access Token | Client (localStorage) | 60 minutes | API requests (`Authorization: Bearer`) |
| Refresh Token | HTTP-only cookie | 7 days | Token refresh |
| API Key | Request header | No expiry | Scheduled tasks (`X-Api-Key`) |

### Auth Attributes

```csharp
[Authorize]                    // Requires valid JWT
[AllowAnonymous]              // Public endpoint
[Authorize(Roles = "Admin")]  // Admin only
```

### Getting Current User

```csharp
// In controller - get user ID from claims
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

// In command/query - pass from controller
public record CreateLeagueCommand(string Name, string UserId) : IRequest<LeagueDto>;
```

## Error Handling

### ErrorHandlingMiddleware

Exceptions are automatically mapped to HTTP responses:

| Exception | Status Code | Use When |
|-----------|-------------|----------|
| `KeyNotFoundException` | 404 Not Found | Entity not found |
| `EntityNotFoundException` | 404 Not Found | Custom not found |
| `ArgumentException` | 400 Bad Request | Invalid argument |
| `InvalidOperationException` | 400 Bad Request | Invalid state/action |
| `ValidationException` | 400 Bad Request | FluentValidation failure |
| `UnauthorizedAccessException` | 401 Unauthorized | Auth failure |
| Other | 500 Internal Error | Unexpected errors |

### Throwing Errors in Handlers

```csharp
// Not found
if (league is null)
    throw new KeyNotFoundException($"League with ID {id} not found");

// Invalid operation
if (!round.CanAcceptPredictions())
    throw new InvalidOperationException("Round is not accepting predictions");

// Unauthorised
if (league.AdministratorUserId != userId)
    throw new UnauthorizedAccessException("Only the administrator can perform this action");
```

## Scheduled Task Endpoints

All `/api/tasks/*` endpoints are protected by API key.

| Endpoint | Purpose | Frequency |
|----------|---------|-----------|
| `POST /api/tasks/publish-upcoming-rounds` | Publish rounds ready for predictions | Daily 9am |
| `POST /api/tasks/send-reminders` | Email reminders for upcoming deadlines | Every 30 min |
| `POST /api/tasks/sync-season` | Sync fixture data from Football API | Daily 8am |
| `POST /api/tasks/update-live-scores` | Update scores during matches | Every minute |

### Task Controller Pattern

```csharp
[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    [HttpPost("publish-upcoming-rounds")]
    [ApiKeyAuth]  // Custom attribute for X-Api-Key validation
    public async Task<IActionResult> PublishUpcomingRounds(CancellationToken ct)
    {
        await _mediator.Send(new PublishUpcomingRoundsCommand(), ct);
        return Ok();
    }
}
```

## Validation

Validation happens automatically via the `ValidationBehaviour` pipeline:

1. Request comes in
2. FluentValidation validator runs (if exists)
3. If invalid, throws `ValidationException` → 400 response
4. If valid, handler executes

You don't need to manually validate in controllers or handlers.
