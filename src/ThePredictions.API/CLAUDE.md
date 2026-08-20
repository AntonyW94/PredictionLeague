# API Project Guidelines

Rules specific to the REST API project. For solution-wide patterns, see the root [`CLAUDE.md`](../../CLAUDE.md).

## Controller Organisation

```
/api/authentication → Authentication (login, register, refresh)
/api/account        → User profile
/api/boosts         → Boost management
/api/dashboard      → Dashboard data
/api/leagues        → League CRUD and membership
/api/predictions    → Prediction submission
/api/rounds         → Round queries
/api/admin/rounds   → Admin round management
/api/admin/seasons  → Admin season management
/api/external/tasks → Background job triggers (API key protected) [also /api/tasks]
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
| JWT Access Token | Client (localStorage) | 15 minutes | API requests (`Authorization: Bearer`) |
| Refresh Token | HTTP-only cookie | 30 days (sliding) | Token refresh |
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
| `BusinessRuleViolationException` | 400 Bad Request | A rule the caller could have satisfied |
| `ValidationException` | 400 Bad Request | FluentValidation failure |
| `UnauthorizedAccessException` | 401 Unauthorized | Auth failure |
| `SeasonPassRequiredException` | 402 Payment Required | The season needs a pass this account does not hold |
| `EmailNotConfirmedException` | 403 Forbidden | The account has not confirmed its email address |
| `ReadQueryFailedException` | 500 Internal Error | A read query failed to execute or materialise |
| `InvalidOperationException` | 500 Internal Error | A server-side defect - missing setting, misused API |
| Other | 500 Internal Error | Unexpected errors |

**Business rules throw `BusinessRuleViolationException`** (400 and a Warning) - the caller asked for something the current state does not allow, so the fault is the request's. `InvalidOperationException` is **not** caught by the middleware: it falls through to the unhandled bucket and is reported as an Error with a 500.

That split is deliberately fail-safe. An unclassified fault is reported as a server problem, which is the assumption that degrades gracefully - the reverse default hid real breakage (a missing Stripe key, a result set that would not materialise) in the client-error bucket, where no alert looks for it and the 400 tells the user they did something wrong. See [ADR-0016](../../docs/decisions/0016-business-rule-exception-classification.md).

Never throw `BusinessRuleViolationException` for an infrastructure or configuration failure, and never assume a bare `InvalidOperationException` from a library means a client mistake.

### Log severity says who has to act

**Every client fault in that table logs at `Information`.** Not Warning. The status code says what happened; the level says whether anybody needs to look. See [ADR-0018](../../docs/decisions/0018-log-severity-says-who-must-act.md).

| Level | Means | Examples |
|-------|-------|----------|
| `Information` | The caller could have made a different request | Wrong id, failed validation, a business rule, an unconfirmed email, an unauthorised attempt, a pass not held |
| `Warning` | Somebody has to act, and it is not the caller's doing | Slow query, missing index, a third party failing or returning nothing, a data condition an administrator must resolve |
| `Error` | Unhandled or unclassified - a defect until proven otherwise | Anything reaching the final `catch` |

This is what makes the warnings alert readable. The `Web Warnings` monitor fires on **more than zero** warnings in five minutes and renotifies every 30 minutes while unresolved, so a bucket that also held routine refusals could not be alerted on - and a real warning arriving among them would not be noticed. `EmailNotConfirmedException` made the case: the same account trips that gate on every attempt until it clicks the link, so one unconfirmed player kept the channel busy on their own.

**A new branch in the middleware goes in at `Information` unless somebody has to act on it.** `ErrorHandlingMiddlewareTests.InvokeAsync_ShouldLogAtInformation_ForEveryClientFault` covers the whole set, so one added at Warning fails the build.

### A missing entity is thrown, never returned as null

**A query handler that cannot find what it was asked for throws `EntityNotFoundException`.** It does
not return `null` for the controller to translate. The middleware above already maps that exception to
404, so the outward behaviour is identical - but the 404 is decided in one place instead of being
re-implemented in every action, and the handler's return type stops being nullable, which means the
compiler enforces the contract rather than a hand-written `if`.

```csharp
// CORRECT - the handler refuses; the middleware answers 404
var league = await dbConnection.QuerySingleOrDefaultAsync<LeagueQueryResult>(sql, cancellationToken, new { request.Id });

if (league is null)
    throw new EntityNotFoundException("League", request.Id);

return new LeagueDto(...);
```

```csharp
// WRONG - a nullable return the caller has to remember to check
public async Task<LeagueDto?> Handle(...) => league is null ? null : new LeagueDto(...);

// ...and in the controller:
if (result == null)
    return NotFound();
```

Two deliberate exceptions, where `null` means "there is no value" rather than "the entity is missing":

- **`GetRoundShareCardImageQueryHandler`** returns `byte[]?`. A round with no fixtures yet has no share
  card to render, which is not the same as the round not existing - throwing "Round was not found"
  there would be untrue.
- **`BadgesController.GetBadgeIcon`** asks `IBadgeIconRenderer` for an unknown badge key on a public,
  anonymous endpoint. It is a renderer returning no image, not a handler failing to find a row, and
  turning crawler traffic into logged exceptions would be a downgrade.

Note that a 404 is also the correct answer when the caller is **not allowed** to see something whose
existence is itself private - `GetLeagueDashboardQueryHandler` throws `EntityNotFoundException` for a
non-member so that the status code cannot be used to discover that a league exists.

### Throwing Errors in Handlers

```csharp
// Not found
if (league is null)
    throw new EntityNotFoundException("League", id);

// Business rule - the caller could have satisfied this
if (!round.CanAcceptPredictions())
    throw new BusinessRuleViolationException("Round is not accepting predictions");

// Server-side defect - a missing setting is not the caller's fault
var templateId = _brevoSettings.Templates?.PasswordReset
    ?? throw new InvalidOperationException("PasswordReset email template ID is not configured");

// Unauthorised
if (league.AdministratorUserId != userId)
    throw new UnauthorizedAccessException("Only the administrator can perform this action");
```

## Scheduled Task Endpoints

All `/api/external/tasks/*` endpoints are protected by API key. The legacy `/api/tasks/*` routes also work for backwards compatibility.

| Endpoint | Purpose | Frequency |
|----------|---------|-----------|
| `POST /api/external/tasks/publish-upcoming-rounds` | Publish rounds ready for predictions | Daily 9am |
| `POST /api/external/tasks/send-reminders` | Email reminders for upcoming deadlines | Every 30 min |
| `POST /api/external/tasks/sync` | Sync fixture data from Football API | Daily 8am |
| `POST /api/external/tasks/score-update` | Update scores during matches | Every minute |
| `POST /api/external/tasks/freeze-prizes` | Freeze prize schemes for leagues past their entry deadline | Hourly |
| `POST /api/external/tasks/send-welcome-emails` | Send league welcome emails once the entry deadline passes (7-day window) | Hourly, after freeze-prizes |

### Task Controller Pattern

```csharp
[ApiController]
[Route("api/external/tasks")]
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
