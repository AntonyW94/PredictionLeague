# Error Contract Standardisation

## Status

**Not Started** | In Progress | Complete

## Priority

**High** - The API currently emits at least six different error body shapes, so the Blazor client probes JSON blindly (`errors[0].ErrorMessage`, then `["message"]`) in seven-plus places, and authorisation failures return 401 where the Swagger annotations already document 403. This plan implements and extends item 4.4 (exception taxonomy) of the June 2026 code review: [`docs/todo/architecture/code-consistency-audit/2026-06-code-review-findings.md`](../code-consistency-audit/2026-06-code-review-findings.md). It also delivers the `ApiErrorResponse` DTO designed in Task 2 of the [api-documentation plan](../api-documentation/README.md), which cross-references back here.

**Read the June findings item 4.4 before starting.** This plan covers every bullet of 4.4 plus the wire-contract work 4.4 deferred ("decide whether the middleware should map it to 403 rather than 401" - decided: yes, via a new exception type).

Related plans to coordinate with:

- [`docs/todo/architecture/api-documentation/README.md`](../api-documentation/README.md) Task 2 - designed the `ApiErrorResponse` DTO; this plan creates it. That plan keeps only the Swagger examples/attributes work.
- [`docs/todo/security/server-validation-gap/README.md`](../../security/server-validation-gap/README.md) - being rewritten in parallel to actually wire server-side FluentValidation (June finding 1.1). The middleware written here maps `FluentValidation.ValidationException` to a 400 `ApiErrorResponse` with the `Errors` dictionary populated; the mapping is inert until that plan wires validation, and required by it once it does. Land this middleware first or together.
- [`docs/todo/architecture/test-suite/README.md`](../test-suite/README.md) - Phase 5 (API tests via `WebApplicationFactory`) is still outstanding; the middleware tests in this plan are plain unit tests and do NOT depend on it (see Task 1.7).

## Conventions that apply throughout

- UK English in all identifiers, strings and comments (`standardise`, `authorisation`, `serialise`). Framework type names (`UnauthorizedAccessException`) keep their framework spelling.
- Plain hyphens only; never em dashes or en dashes, including in log templates and user-facing messages.
- One public type per file.
- Statements on a new line after `if`.
- Test naming `MethodName_ShouldX_WhenY()`; pass `CancellationToken.None`, never a bare `default` (xUnit1051 fails CI under `/p:TreatWarningsAsErrors=true`).
- No database changes are involved; no migration or schema-doc updates needed.

---

## 1. Current state (verified 2026-07-06 - re-verify before starting)

### 1.1 The middleware emits six ad-hoc shapes

`src\ThePredictions.API\Middleware\ErrorHandlingMiddleware.cs` (89 lines) currently maps:

| Lines | Exception(s) | Status | Body shape |
|-------|--------------|--------|------------|
| 16-20 | `KeyNotFoundException`, `ArgumentNullException`, `EntityNotFoundException` | 404 | `{ message }` |
| 21-25 | `SeasonPassRequiredException` | 402 | `{ message, seasonId }` |
| 26-30 | `EmailNotConfirmedException` | 403 | `{ message, emailNotConfirmed: true }` |
| 31-35 | `ArgumentException` | 400 | `{ message }` |
| 36-40 | `InvalidOperationException` | 400 | `{ message }` |
| 41-45 | `FluentValidation.ValidationException` | 400 | `{ errors }` (serialised `ValidationFailure` array) |
| 46-50 | `IdentityUpdateException` | 400 | `{ errors }` (string array) |
| 51-55 | `UnauthorizedAccessException` | 401 | `{ message: "You are not authorised to perform this action." }` (fixed text) |
| 56-59 | `IOException` with message "The client reset the request stream" | none | logs only, no body |
| 60-63 | any exception whose `ex.Message.Contains("A task was canceled")` | none | logs only, no body (June 4.4: should catch `OperationCanceledException`) |
| 64-68 | everything else | 500 | `{ message, details }` (`details` = stack trace in Development, `null` otherwise) |

Serialisation is `JsonSerializer.Serialize(errorResponse)` with no options; the anonymous-object property names are already lowercase (`message`, `errors`, `seasonId`), which the replacement must preserve via camelCase options.

### 1.2 A second error contract coexists: result objects returned by controllers

- `src\ThePredictions.API\Controllers\AuthenticationController.cs`:
  - line 54 (register): `return result.IsSuccess ? Ok(result) : BadRequest(result);`
  - lines 108-109 (login): `if (result is not SuccessfulAuthenticationResponse success) return Unauthorized(result);`
  - line 156 (refresh, token missing): `return BadRequest(new { message = "Refresh token is missing." });`
  - line 169 (refresh, token rejected): `return BadRequest(result);`
  - lines 241-242 (reset-password): `if (result is not SuccessfulResetPasswordResponse success) return BadRequest(result);`
  - The failure DTO is `FailedAuthenticationResponse(string Message) : AuthenticationResponse(false)` (`src\ThePredictions.Contracts\Authentication\FailedAuthenticationResponse.cs`).
- `src\ThePredictions.API\Controllers\BoostsController.cs` lines 46-50 (apply): returns `Ok(result)` or `BadRequest(result)` with the same `ApplyBoostResultDto` (`Success`, `Error`, `AlreadyUsedThisRound`) on both paths.

### 1.3 The client consequently probes multiple shapes

- `src\ThePredictions.Web.Client\Components\Shared\BaseFormComponent.razor` lines 69-88: on 400 only, reads a `JsonNode`, tries `errors[0].ErrorMessage` (matches the serialised `ValidationFailure` array), falls back to `message`, then to a generic string.
- `src\ThePredictions.Web.Client\Services\Leagues\LeagueService.cs`: reads `errorContent?["message"]` via `JsonNode` in five methods: `JoinPublicLeagueAsync` (line 106), `JoinPrivateLeagueAsync` (line 161), `CancelJoinRequestAsync` (line 195), `DismissAlertAsync` (line 213), `SetLeagueArchivedAsync` (line 232).
- `src\ThePredictions.Web.Client\Services\SeasonPasses\SeasonPassService.cs` line 47: same `["message"]` probe in `AcquireAsync`.
- `src\ThePredictions.Web.Client\Authentication\AuthenticationService.cs`: register failure tries a private `IdentityErrorResponse { List<string> Errors }` shape first (lines 29-35, matches the `IdentityUpdateException` body), then `FailedAuthenticationResponse` (lines 37-45); login and reset-password deserialise `FailedAuthenticationResponse` / `FailedResetPasswordResponse`.
- `src\ThePredictions.Web.Client\Services\Boosts\BoostClientService.cs` lines 27-32: reads `ApplyBoostResultDto` from both success and failure bodies.
- `src\ThePredictions.Web.Client\Authentication\ApiAuthenticationStateProvider.cs` line 196: the refresh flow treats **400 or 401 from the refresh endpoint as "session over"** and anything else as transient. It never reads the body. **This status-code contract MUST be preserved.**

### 1.4 Who consumes the special fields? (investigated - nobody)

- `seasonId` in the 402 body: grep of `ThePredictions.Web.Client` for `seasonId` finds only routine query-string/DTO usage; **no code reads `seasonId` out of an error body**. `SeasonPassService.AcquireAsync` reads only `message`.
- `emailNotConfirmed` in the 403 body: grep of the client for `emailNotConfirmed` / `EmailNotConfirmed` / `PaymentRequired` / `402` finds **nothing**. Moreover `EmailNotConfirmedException` is **never thrown anywhere in `src`** (only caught by the middleware and defined in `src\ThePredictions.Domain\Common\Exceptions\EmailNotConfirmedException.cs`); the catch block is currently dead code. Keep the mapping (the exception documents an intended flow) but nothing constrains its shape.

Decision: neither field needs a bespoke top-level property. Preserve the information via dedicated `Code` values (`SEASON_PASS_REQUIRED`, `EMAIL_NOT_CONFIRMED`) plus a generic optional `Extensions` dictionary carrying `seasonId`. No client change is needed for these two cases.

### 1.5 Wrong status codes and stray exception types (June 4.4)

- Permission failures throw `UnauthorizedAccessException` and surface as **401**, while Swagger already documents **403**, e.g. `src\ThePredictions.API\Controllers\LeaguesController.cs` line 82: `[SwaggerResponse(403, "Not a member of this league")]` on `GetLeagueByIdAsync`.
- `src\ThePredictions.Application\Features\Leagues\Commands\DeleteLeagueCommandHandler.cs` line 17 throws `System.Security.Authentication.AuthenticationException`, which no catch block maps, so it surfaces as **500**.
- `src\ThePredictions.API\Services\CurrentUserService.cs` `EnsureAdministrator()` throws `UnauthorizedAccessException` for **both** "not authenticated" (line 18, genuinely 401) and "not an administrator" (line 21, should be 403).
- Three query handlers throw `KeyNotFoundException` instead of `EntityNotFoundException`: `GetLeagueBankDetailsQueryHandler.cs:31`, `GetLeaguePaymentInfoQueryHandler.cs:44`, `GetLeaguePayoutsQueryHandler.cs:31`.
- `src\ThePredictions.Application\Features\Admin\Users\Commands\DeleteUserCommandHandler.cs` line 34 uses the Ardalis guard `Guard.Against.NotFound(request.NewAdministratorId, newAdmin, "New Administrator User")`, which throws `Ardalis.GuardClauses.NotFoundException` - **not caught by the middleware, so it currently surfaces as 500, not 404**. (Line 23 of the same file already uses the correct domain guard `Guard.Against.EntityNotFound`.)
- Raw `throw new Exception(...)` (surfaces as 500): `DeleteUserCommandHandler.cs:45` and `UpdateUserRoleCommandHandler.cs:23`. Both have `UserManagerResult.Errors` (`IEnumerable<string>`) available and should throw `IdentityUpdateException` (which exists: `src\ThePredictions.Application\Common\Exceptions\IdentityUpdateException.cs`, the only file in that folder, namespace `ThePredictions.Application.Common.Exceptions`).
- Cancellation is caught by message matching (`ex.Message.Contains("A task was canceled")`, middleware lines 60-63) instead of catching `OperationCanceledException`.

### 1.6 Infrastructure prerequisites (verified)

- `src\ThePredictions.API\Program.cs` lines 18-19 register `CorrelationIdMiddleware` **before** `ErrorHandlingMiddleware`, so the correlation id is always available to the error handler.
- `src\ThePredictions.API\Middleware\CorrelationIdMiddleware.cs` stores it in `context.Items[CorrelationIdMiddleware.LogPropertyName]` where `LogPropertyName = "CorrelationId"` (both constants are `internal`).
- The Web.Client project already references `ThePredictions.Contracts` (it uses `ThePredictions.Contracts.*` DTOs throughout), so the client can deserialise `ApiErrorResponse` directly.
- There is **no API test project**: `tests\Unit\` contains only `ThePredictions.Application.Tests.Unit`, `ThePredictions.Composition.Tests.Unit`, `ThePredictions.Domain.Tests.Unit`, `ThePredictions.Validators.Tests.Unit`, `ThePredictions.Web.Client.Tests.Unit`. Task 1.7 creates `ThePredictions.API.Tests.Unit`.

---

## 2. Target design

### 2.1 The single wire shape: `ApiErrorResponse`

Adopt the DTO exactly as designed in the api-documentation plan Task 2.1, with **one addition**: an optional `Extensions` dictionary (needed to carry `seasonId` for 402, section 1.4).

**New file:** `src\ThePredictions.Contracts\Common\ApiErrorResponse.cs`

```csharp
namespace ThePredictions.Contracts.Common;

/// <summary>
/// Standard error response returned by all API endpoints.
/// </summary>
public record ApiErrorResponse
{
    /// <summary>
    /// Machine-readable error code (e.g. "VALIDATION_ERROR", "NOT_FOUND"). See ApiErrorCodes.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Detailed validation errors, keyed by field name. Only present for 400 responses.
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; init; }

    /// <summary>
    /// Additional machine-readable context for specific codes
    /// (e.g. "seasonId" for SEASON_PASS_REQUIRED). Omitted when not applicable.
    /// </summary>
    public Dictionary<string, object>? Extensions { get; init; }

    /// <summary>
    /// Correlation ID for tracking in logs.
    /// </summary>
    public string? TraceId { get; init; }
}
```

The api-documentation plan's README now notes that this plan owns the DTO and middleware; keep the two documents in lockstep if the shape ever changes.

**Why not RFC 7807 `ProblemDetails`?** ASP.NET Core ships `ProblemDetails`/`ValidationProblemDetails` and RFC 7807 is the textbook answer, but adopting it here would (a) diverge from the already-designed and cross-referenced api-documentation plan, (b) rename every field the client parses (`message` becomes `detail`, `errors` moves under an extension), forcing a bigger, riskier client migration for zero functional gain on an internal API with a single first-party consumer, and (c) drag in the `application/problem+json` content type that the existing client helpers do not expect. The chosen shape keeps `message` at the top level, which every existing client probe already reads, making each PR below independently deployable. Trade-off accepted: third-party consumers would prefer RFC 7807; if the API ever goes public, revisit with a new plan and supersede this decision.

### 2.2 Machine-readable codes: `ApiErrorCodes`

**New file:** `src\ThePredictions.Contracts\Common\ApiErrorCodes.cs` (one public type per file)

```csharp
namespace ThePredictions.Contracts.Common;

/// <summary>
/// The fixed set of machine-readable values for <see cref="ApiErrorResponse.Code"/>.
/// </summary>
public static class ApiErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string IdentityError = "IDENTITY_ERROR";
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string SeasonPassRequired = "SEASON_PASS_REQUIRED";
    public const string EmailNotConfirmed = "EMAIL_NOT_CONFIRMED";
    public const string ServerError = "SERVER_ERROR";
}
```

| Code | Status | Produced by |
|------|--------|-------------|
| `VALIDATION_ERROR` | 400 | `FluentValidation.ValidationException` (populated `Errors` dictionary) |
| `IDENTITY_ERROR` | 400 | `IdentityUpdateException` (`Errors` under key `"identity"`) |
| `BAD_REQUEST` | 400 | `ArgumentException`, `InvalidOperationException`, controller-level failures with no better code |
| `UNAUTHENTICATED` | 401 (400 on the refresh endpoint, see Task 1.5) | `UnauthorizedAccessException`, auth endpoint failures |
| `FORBIDDEN` | 403 | new `ForbiddenAccessException` |
| `NOT_FOUND` | 404 | `EntityNotFoundException` (plus legacy `KeyNotFoundException`/`ArgumentNullException`) |
| `CONFLICT` | 409 | reserved; nothing maps to 409 today, defined so future handlers do not invent a new string |
| `SEASON_PASS_REQUIRED` | 402 | `SeasonPassRequiredException`; `Extensions["seasonId"]` carries the season id |
| `EMAIL_NOT_CONFIRMED` | 403 | `EmailNotConfirmedException` |
| `SERVER_ERROR` | 500 | anything unhandled |

Serialisation rule: the middleware serialises with `JsonSerializerDefaults.Web` (camelCase) and `DefaultIgnoreCondition = WhenWritingNull`, so the wire fields are `code`, `message`, `errors`, `extensions`, `traceId`. This keeps `message` lowercase, which every existing client probe depends on (section 1.3), making the server change deployable before the client change.

---

## 3. Task 1 (PR 1, server): `ForbiddenAccessException` + middleware rewrite

### 3.1 Create `ForbiddenAccessException`

The folder `src\ThePredictions.Application\Common\Exceptions\` exists and contains only `IdentityUpdateException.cs` (namespace `ThePredictions.Application.Common.Exceptions`, primary-constructor style). Match it.

**New file:** `src\ThePredictions.Application\Common\Exceptions\ForbiddenAccessException.cs`

```csharp
namespace ThePredictions.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated user attempts an action they do not have permission to
/// perform (e.g. a non-administrator calling a league-administrator action). Mapped to
/// 403 Forbidden by the API's ErrorHandlingMiddleware. For "not logged in at all", throw
/// UnauthorizedAccessException instead (mapped to 401).
/// </summary>
public class ForbiddenAccessException(string message) : Exception(message);
```

It lives in Application (not Domain) because permission checks are application concerns; API and Infrastructure both reference Application, so all throw sites in Task 3 can reach it. No `[ExcludeFromCodeCoverage]` needed (the 100% rule applies to Domain only), and it has no logic to test.

### 3.2 Create `ApiErrorResponse` and `ApiErrorCodes`

Exactly as in sections 2.1 and 2.2, in `src\ThePredictions.Contracts\Common\` (create the `Common` folder; it does not exist yet).

### 3.3 Rewrite `ErrorHandlingMiddleware`

Replace the entire contents of `src\ThePredictions.API\Middleware\ErrorHandlingMiddleware.cs` with:

```csharp
using FluentValidation;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.API.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentNullException or EntityNotFoundException)
        {
            logger.LogWarning("Not Found Error: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.NotFound, ApiErrorCodes.NotFound, ex.Message);
        }
        catch (SeasonPassRequiredException ex)
        {
            logger.LogWarning("Season Pass Required: {Message}", ex.Message);
            await WriteErrorAsync(
                context,
                HttpStatusCode.PaymentRequired,
                ApiErrorCodes.SeasonPassRequired,
                ex.Message,
                extensions: new Dictionary<string, object> { ["seasonId"] = ex.SeasonId });
        }
        catch (EmailNotConfirmedException ex)
        {
            logger.LogWarning("Email Not Confirmed: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.Forbidden, ApiErrorCodes.EmailNotConfirmed, ex.Message);
        }
        catch (ForbiddenAccessException ex)
        {
            logger.LogWarning("Forbidden: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.Forbidden, ApiErrorCodes.Forbidden, ex.Message);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("Validation Error: {Errors}", ex.Errors);

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationError, "One or more validation errors occurred.", errors);
        }
        catch (IdentityUpdateException ex)
        {
            logger.LogWarning("Identity Update Error: {Errors}", ex.Errors);

            var errors = new Dictionary<string, string[]> { ["identity"] = ex.Errors.ToArray() };

            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ApiErrorCodes.IdentityError, ex.Message, errors);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning("Invalid Argument/Business Rule Error: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ApiErrorCodes.BadRequest, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Authentication Error: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, ApiErrorCodes.Unauthenticated, "Authentication is required to perform this action.");
        }
        catch (IOException ex) when (ex.Message.Contains("The client reset the request stream"))
        {
            logger.LogInformation("Client reset the request stream. Request path: {Path}", context.Request.Path);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected or gave up; there is nobody to respond to.
            logger.LogInformation("Request cancelled by the client. Request path: {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception has occurred.");

            var message = env.IsDevelopment() ? ex.Message : "An internal server error has occurred.";
            var extensions = env.IsDevelopment() && ex.StackTrace is not null
                ? new Dictionary<string, object> { ["details"] = ex.StackTrace }
                : null;

            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, ApiErrorCodes.ServerError, message, extensions: extensions);
        }
    }

    private Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string code,
        string message,
        Dictionary<string, string[]>? errors = null,
        Dictionary<string, object>? extensions = null)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning("The response has already started; the {Code} error body cannot be written.", code);
            return Task.CompletedTask;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ApiErrorResponse
        {
            Code = code,
            Message = message,
            Errors = errors,
            Extensions = extensions,
            TraceId = context.Items[CorrelationIdMiddleware.LogPropertyName] as string
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }
}
```

Design notes (each deliberate, do not "simplify" away):

- **Cancellation** is now caught by type, per June 4.4. The `when (context.RequestAborted.IsCancellationRequested)` filter means only client-initiated cancellations are swallowed silently; an `OperationCanceledException` from an internal timeout (client still connected) falls through to the generic 500 handler, which is the correct signal. Nothing is ever written if the response has started (`WriteErrorAsync` guard).
- **TraceId** comes from `context.Items["CorrelationId"]`, populated by `CorrelationIdMiddleware`, which `Program.cs` registers first (lines 18-19). Both middlewares are in the same assembly so the `internal` constant is accessible.
- **Catch order matters**: `ArgumentNullException` must stay in the not-found filter above the `ArgumentException` filter (it derives from it); `OperationCanceledException` must precede the generic catch; `ForbiddenAccessException` before nothing in particular but keep specific types before general filters.
- The 401 body keeps a **fixed** message (never echo `ex.Message`, which can carry internal detail); the old fixed text said "not authorised", which after Task 3 is wrong for a 401, hence the new "Authentication is required to perform this action."
- **`ValidationFailure` grouping**: the `Errors` dictionary is keyed by `PropertyName` with all messages for that property, matching the shape the api-documentation plan documents in its `ValidationErrorExample`. This is the contract the server-validation-gap plan must emit through.

### 3.4 Controller-created errors: `ApiErrorFactory`

Controllers that hand-build failure bodies need the same shape and `TraceId`.

**New file:** `src\ThePredictions.API\Common\ApiErrorFactory.cs`

```csharp
using ThePredictions.API.Middleware;
using ThePredictions.Contracts.Common;

namespace ThePredictions.API.Common;

public static class ApiErrorFactory
{
    public static ApiErrorResponse Create(HttpContext context, string code, string message)
    {
        return new ApiErrorResponse
        {
            Code = code,
            Message = message,
            TraceId = context.Items[CorrelationIdMiddleware.LogPropertyName] as string
        };
    }
}
```

### 3.5 Standardise the result-object failure paths (AuthenticationController)

Keep the success DTOs (`SuccessfulAuthenticationResponse` etc.) untouched. Change only the **failure** bodies. Per endpoint in `src\ThePredictions.API\Controllers\AuthenticationController.cs`:

| Endpoint | Current failure | New failure |
|----------|-----------------|-------------|
| `POST register` (line 54) | `BadRequest(result)` (a `FailedAuthenticationResponse`) | 400 + `ApiErrorFactory.Create(HttpContext, ApiErrorCodes.BadRequest, message)` where `message` is `(result as FailedAuthenticationResponse)?.Message ?? "Registration could not be completed."` |
| `POST login` (lines 108-109) | `Unauthorized(result)` | 401 + `ApiErrorFactory.Create(HttpContext, ApiErrorCodes.Unauthenticated, message)` where `message` is `(result as FailedAuthenticationResponse)?.Message ?? "Invalid email or password."` |
| `POST refresh-token`, token missing (line 156) | `BadRequest(new { message = "Refresh token is missing." })` | **still 400** + `ApiErrorFactory.Create(HttpContext, ApiErrorCodes.Unauthenticated, "Refresh token is missing.")` |
| `POST refresh-token`, token rejected (line 169) | `BadRequest(result)` | **still 400** + `ApiErrorFactory.Create(HttpContext, ApiErrorCodes.Unauthenticated, message)` from the `FailedAuthenticationResponse` |
| `POST reset-password` (lines 241-242) | `BadRequest(result)` (a `FailedResetPasswordResponse`) | 400 + `ApiErrorFactory.Create(HttpContext, ApiErrorCodes.BadRequest, message)` from the failure DTO's `Message` |

Explicit contract statements:

- **The refresh flow's status codes do not change.** `ApiAuthenticationStateProvider.RefreshAccessTokenAsync` (line 196) treats 400/401 as end-of-session and anything else as transient, and never reads the body. Both refresh failure paths stay 400; only the body shape changes, which that code path never parses. This preserves the "400 means log in again" contract exactly.
- The anonymous **success** bodies (`Ok(new { message = ... })` on confirm-email line 74, resend-confirmation line 90, forgot-password line 218) are 200 responses, not errors; they are out of scope and unchanged.
- **Deployed-client compatibility during rollout** (why PR 1 can ship before the client PR): the client deserialises failure bodies into `FailedAuthenticationResponse(string Message)` case-insensitively; an `ApiErrorResponse` body supplies `message`, so `Message` still populates and `IsSuccess` defaults to `false`. The register path first probes `{ errors: [...] }` as a `List<string>`; the new body either has no `errors` (probe finds nothing, falls through) or a dictionary (deserialisation throws `JsonException`, which is caught at lines 33-35 and falls through to the `message` probe). Net effect: identical UX, except identity-error detail is summarised as "One or more Identity errors occurred." until the client PR lands. Acceptable and temporary.

### 3.6 Update `src\ThePredictions.API\CLAUDE.md`

Replace the Error Handling mapping table (currently listing `UnauthorizedAccessException` as "401 Unauthorized / Auth failure") with:

```markdown
### ErrorHandlingMiddleware

All error responses share one body: `ApiErrorResponse` (`ThePredictions.Contracts.Common`) with
`code`, `message`, optional `errors` dictionary, optional `extensions`, and `traceId`.
Codes are the constants in `ApiErrorCodes`.

| Exception | Status Code | Code | Use When |
|-----------|-------------|------|----------|
| `ValidationException` (FluentValidation) | 400 Bad Request | `VALIDATION_ERROR` | Request fails validation |
| `IdentityUpdateException` | 400 Bad Request | `IDENTITY_ERROR` | ASP.NET Identity operation failed |
| `ArgumentException` | 400 Bad Request | `BAD_REQUEST` | Invalid argument |
| `InvalidOperationException` | 400 Bad Request | `BAD_REQUEST` | Invalid state/action |
| `UnauthorizedAccessException` | 401 Unauthorized | `UNAUTHENTICATED` | Not logged in / no valid identity |
| `SeasonPassRequiredException` | 402 Payment Required | `SEASON_PASS_REQUIRED` | No Season Pass for the season (`extensions.seasonId`) |
| `ForbiddenAccessException` | 403 Forbidden | `FORBIDDEN` | Logged in but not permitted |
| `EmailNotConfirmedException` | 403 Forbidden | `EMAIL_NOT_CONFIRMED` | Email not yet verified |
| `EntityNotFoundException` | 404 Not Found | `NOT_FOUND` | Entity not found (preferred) |
| `KeyNotFoundException` | 404 Not Found | `NOT_FOUND` | Legacy - use `EntityNotFoundException` |
| Other | 500 Internal Error | `SERVER_ERROR` | Unexpected errors |
```

Also update the "Throwing Errors in Handlers" snippet in the same file: the "Unauthorised" example (line 119, `throw new UnauthorizedAccessException("Only the administrator can perform this action");`) becomes `throw new ForbiddenAccessException("Only the administrator can perform this action.");`, and the not-found example should use `EntityNotFoundException` (or `Guard.Against.EntityNotFound`).

### 3.7 Middleware unit tests: new project `ThePredictions.API.Tests.Unit`

There is no API test project (section 1.6), and the test-suite plan's `WebApplicationFactory` phase is not a prerequisite: `ErrorHandlingMiddleware` is a plain class testable with `DefaultHttpContext`. **Create the project now**; it becomes the natural home for future API-layer unit tests.

1. **New file:** `tests\Unit\ThePredictions.API.Tests.Unit\ThePredictions.API.Tests.Unit.csproj`, mirroring `tests\Unit\ThePredictions.Application.Tests.Unit\ThePredictions.Application.Tests.Unit.csproj` (same `PropertyGroup`; same package versions already used across the test suite: `coverlet.collector` 6.0.4, `xunit.v3` 3.0.0, `xunit.runner.visualstudio` 3.0.0, `FluentAssertions` 7.0.0, `Microsoft.NET.Test.Sdk` 17.11.0, `NSubstitute` 5.3.0 - these are existing solution packages, so the "highest available version" rule for new packages does not bite; consistency wins), plus:

   ```xml
   <ItemGroup>
     <FrameworkReference Include="Microsoft.AspNetCore.App" />
   </ItemGroup>

   <ItemGroup>
     <ProjectReference Include="..\..\..\src\ThePredictions.API\ThePredictions.API.csproj" />
   </ItemGroup>
   ```

2. Add to the solution: `dotnet sln ThePredictions.sln add tests\Unit\ThePredictions.API.Tests.Unit\ThePredictions.API.Tests.Unit.csproj`
3. Add `<InternalsVisibleTo Include="ThePredictions.API.Tests.Unit" />` in an `<ItemGroup>` of `src\ThePredictions.API\ThePredictions.API.csproj` so tests can use `CorrelationIdMiddleware.LogPropertyName`.
4. **New file:** `tests\Unit\ThePredictions.API.Tests.Unit\Middleware\ErrorHandlingMiddlewareTests.cs` with a helper and one test per mapping:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;
using ThePredictions.API.Middleware;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.API.Tests.Unit.Middleware;

public class ErrorHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private static async Task<(DefaultHttpContext Context, string Body)> InvokeAsync(
        Exception exceptionToThrow,
        bool isDevelopment = false,
        bool clientCancelled = false)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.LogPropertyName] = "test-correlation-id";

        if (clientCancelled)
            context.RequestAborted = new CancellationToken(canceled: true);

        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns(isDevelopment ? Environments.Development : Environments.Production);

        var middleware = new ErrorHandlingMiddleware(
            _ => throw exceptionToThrow,
            NullLogger<ErrorHandlingMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(CancellationToken.None);
        return (context, body);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnNotFoundApiError_WhenEntityNotFoundExceptionIsThrown()
    {
        var (context, body) = await InvokeAsync(new EntityNotFoundException("League", 42));

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var error = JsonSerializer.Deserialize<ApiErrorResponse>(body, ReadOptions);
        error!.Code.Should().Be(ApiErrorCodes.NotFound);
        error.Message.Should().Be("League (ID: 42) was not found.");
        error.TraceId.Should().Be("test-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_ShouldWriteNoBody_WhenOperationCancelledByClient()
    {
        var (context, body) = await InvokeAsync(new OperationCanceledException(), clientCancelled: true);

        body.Should().BeEmpty();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
```

   Complete the matrix with one `InvokeAsync_ShouldX_WhenY` test each for: `KeyNotFoundException` (404), `SeasonPassRequiredException` (402, `Extensions["seasonId"]` present and correct, code `SEASON_PASS_REQUIRED`), `EmailNotConfirmedException` (403, `EMAIL_NOT_CONFIRMED`), `ForbiddenAccessException` (403, `FORBIDDEN`, message echoed), `FluentValidation.ValidationException` built from two `ValidationFailure`s on the same property plus one on another (400, `VALIDATION_ERROR`, dictionary grouping asserted), `IdentityUpdateException` (400, `IDENTITY_ERROR`, `Errors["identity"]`), `ArgumentException` and `InvalidOperationException` (400, `BAD_REQUEST`), `UnauthorizedAccessException` (401, `UNAUTHENTICATED`, fixed message, exception message NOT echoed), `OperationCanceledException` **without** client cancellation (500, `SERVER_ERROR`), generic `Exception` in Production (500, message is the generic text, no `extensions`) and in Development (500, `extensions.details` present), and content type `application/json` on any written response. Add one test where a fake `IHttpResponseFeature` reports `HasStarted = true` and assert nothing is written.

   Note the `Extensions` values deserialise as `JsonElement`; assert via `((JsonElement)error.Extensions!["seasonId"]).GetInt32().Should().Be(5)`.

### 3.8 PR 1 verification

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests\Unit\ThePredictions.API.Tests.Unit
dotnet test tests\Unit\ThePredictions.Application.Tests.Unit
dotnet test tests\Unit\ThePredictions.Web.Client.Tests.Unit
```

All must pass. PR 1 is deployable on its own: every changed body still exposes lowercase `message`, all status codes are unchanged (403s only start appearing after Task 3), and the refresh contract is untouched.

---

## 4. Task 2 (PR 2, server + client): shared parsing, remove probing

### 4.1 Shared client helper

**New file:** `src\ThePredictions.Web.Client\Services\Common\ApiErrorParser.cs`

```csharp
using System.Net.Http.Json;
using ThePredictions.Contracts.Common;

namespace ThePredictions.Web.Client.Services.Common;

/// <summary>
/// Reads the standard ApiErrorResponse body from a failed HTTP response.
/// All failure-path parsing in the client goes through here; do not probe raw JSON.
/// </summary>
public static class ApiErrorParser
{
    public static async Task<ApiErrorResponse?> TryReadAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, string fallback)
    {
        var error = await TryReadAsync(response);
        if (error is null)
            return fallback;

        var firstFieldError = error.Errors?.Values.SelectMany(v => v).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstFieldError))
            return firstFieldError;

        return string.IsNullOrWhiteSpace(error.Message) ? fallback : error.Message;
    }
}
```

### 4.2 `BaseFormComponent.razor`

Replace the whole `else` block (lines 67-92, the 400-only `JsonNode` probing quoted in section 1.3) with:

```csharp
else
{
    ErrorMessage = await ApiErrorParser.ReadErrorMessageAsync(response, ErrorAlertMessage);
}
```

(add `@using ThePredictions.Web.Client.Services.Common` at the top). The `if (string.IsNullOrWhiteSpace(ErrorMessage)) ErrorMessage = ErrorAlertMessage;` line becomes redundant and is removed. Behaviour improves slightly: non-400 failures now show the server message instead of always the generic one.

### 4.3 `LeagueService.cs` and `SeasonPassService.cs`

In `src\ThePredictions.Web.Client\Services\Leagues\LeagueService.cs`, replace each of the five try/catch `JsonNode` blocks (section 1.3) with a single line, e.g. for `JoinPublicLeagueAsync`:

```csharp
return (false, await ApiErrorParser.ReadErrorMessageAsync(response, "An unknown error occurred while trying to join the league."));
```

Keep each method's existing fallback string. Remove `using System.Text.Json.Nodes;` once no longer referenced. Apply the same one-line replacement in `SeasonPassService.AcquireAsync` (`src\ThePredictions.Web.Client\Services\SeasonPasses\SeasonPassService.cs` lines 44-53).

### 4.4 `AuthenticationService.cs`

In `src\ThePredictions.Web.Client\Authentication\AuthenticationService.cs`:

- `LoginAsync` failure path (lines 64-74): replace with `return new FailedAuthenticationResponse(await ApiErrorParser.ReadErrorMessageAsync(response, "An unexpected error occurred during login."));`
- `RegisterAsync` failure path (lines 25-47): delete the `IdentityErrorResponse` probe and the private `IdentityErrorResponse` class entirely; replace with `return new FailedAuthenticationResponse(await ApiErrorParser.ReadErrorMessageAsync(response, "An unexpected error occurred during registration."));` (the helper surfaces the first identity error from the `Errors` dictionary, restoring the specific-error detail that was transiently generic after PR 1).
- `ResetPasswordAsync` failure path (lines 118-128): replace with `return new FailedResetPasswordResponse(await ApiErrorParser.ReadErrorMessageAsync(response, "An error occurred. Please try again."));`

The pages (`Login.razor`, `Register.razor`, `ResetPassword.razor`) consume `FailedAuthenticationResponse.Message` / `FailedResetPasswordResponse.Message` and need no changes.

### 4.5 `BoostsController` apply failure (the one server change in this PR)

`ApplyBoostResultDto.AlreadyUsedThisRound` is never read from the apply **failure** body anywhere in the client (verified: the client reads `AlreadyUsedThisRound` only from `BoostEligibilityDto` in the available-boosts list), so the failure body can be standardised. In `src\ThePredictions.API\Controllers\BoostsController.cs` `ApplyAsync` (lines 46-50), replace:

```csharp
        if (result.Success)
            return Ok(result);

        return BadRequest(result);
```

with:

```csharp
        if (result.Success)
            return Ok(result);

        var code = result.AlreadyUsedThisRound ? ApiErrorCodes.Conflict : ApiErrorCodes.BadRequest;
        return BadRequest(ApiErrorFactory.Create(HttpContext, code, result.Error ?? "The boost could not be applied."));
```

And in `src\ThePredictions.Web.Client\Services\Boosts\BoostClientService.cs` `ApplyBoostAsync`, replace the failure-body read (lines 31-32) with:

```csharp
            var message = await ApiErrorParser.ReadErrorMessageAsync(result, $"Server returned {result.StatusCode}");
            return new ApplyBoostResultDto { Success = false, Error = message };
```

This server+client pair ships in the same PR/deploy so the boost failure UX never regresses. (Note: the status stays 400 even for the `CONFLICT` code; changing it to 409 would alter observable behaviour for no consumer benefit. The code value carries the semantics.)

### 4.6 Client tests

`tests\Unit\ThePredictions.Web.Client.Tests.Unit` exists. Add `ApiErrorParserTests.cs` covering: `ReadErrorMessageAsync_ShouldReturnMessage_WhenBodyIsApiErrorResponse`, `ReadErrorMessageAsync_ShouldReturnFirstFieldError_WhenErrorsDictionaryPresent`, `ReadErrorMessageAsync_ShouldReturnFallback_WhenBodyIsNotJson`, `ReadErrorMessageAsync_ShouldReturnFallback_WhenMessageIsEmpty`. Build `HttpResponseMessage` instances directly with `StringContent`; pass `CancellationToken.None` where a token is required.

### 4.7 PR 2 verification

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests\Unit\ThePredictions.Web.Client.Tests.Unit
dotnet test tests\Unit\ThePredictions.API.Tests.Unit
```

Manual smoke: log in with wrong credentials (message shown), join a league twice (message shown), apply a boost twice (specific error shown).

---

## 5. Task 3 (PR 3, server): handler exception sweep

Do this **after** the middleware is live, so each rethrow lands on a mapping that already exists.

### 5.1 Permission failures: `UnauthorizedAccessException` -> `ForbiddenAccessException` (401 -> 403)

Complete enumeration of `UnauthorizedAccessException` throw sites (verified by solution grep; re-run `grep -rn "UnauthorizedAccessException" src/` before starting):

**Keep as `UnauthorizedAccessException` (genuine authentication failure, stays 401):**

| File | Line | Why it stays |
|------|------|--------------|
| `src\ThePredictions.API\Controllers\ApiControllerBase.cs` | 9 | No user id claim in the token: the caller is not properly authenticated |
| `src\ThePredictions.API\Services\CurrentUserService.cs` | 18 (`EnsureAdministrator`, `!IsAuthenticated` branch) | Not logged in at all |

**Change to `ForbiddenAccessException` (authenticated but not permitted, becomes 403):**

| File | Line | Message (unchanged) |
|------|------|---------------------|
| `src\ThePredictions.API\Services\CurrentUserService.cs` | 21 (`!IsAdministrator` branch) | "Administrator privileges are required to access this resource." |
| `src\ThePredictions.Infrastructure\Services\LeagueMembershipService.cs` | 31 | "You must be a member of this league to access this resource." |
| `src\ThePredictions.Infrastructure\Services\LeagueMembershipService.cs` | 55 | "Only the league administrator can access this resource." |
| `src\ThePredictions.Application\Features\Boosts\Commands\SetLeagueBoostRulesCommandHandler.cs` | 32 | "Only the league administrator can set the league's boosts." |
| `src\ThePredictions.Application\Features\Leagues\Queries\GetLeagueBankDetailsQueryHandler.cs` | 34 | "Only the league administrator can view its bank details." |
| `src\ThePredictions.Application\Features\Leagues\Queries\GetLeaguePaymentInfoQueryHandler.cs` | 51 | "Only the league administrator or its members can view payment details." |
| `src\ThePredictions.Application\Features\Leagues\Queries\GetLeaguePayoutsQueryHandler.cs` | 34 | "Only the league administrator can view payouts." |
| `src\ThePredictions.Application\Features\Leagues\Commands\MarkLeaguePayoutPaidCommandHandler.cs` | 26 | "Only the league administrator can mark payouts as paid." |
| `src\ThePredictions.Application\Features\Leagues\Commands\DefinePrizeStructureCommandHandler.cs` | 28 | "The prize structure is now derived from the prize scheme; only a site administrator can set it manually." |
| `src\ThePredictions.Application\Features\Leagues\Queries\GetPrizePreviewQueryHandler.cs` | 22 | "A valid entry code is required to preview this league." |
| `src\ThePredictions.Application\Features\Leagues\Commands\SetPrizeSchemeCommandHandler.cs` | 36 | "Only the league administrator can set the prize scheme." |
| `src\ThePredictions.Application\Features\Leagues\Commands\UpdateLeagueMemberStatusCommandHandler.cs` | 18 | "Only the league administrator can update member status." |
| `src\ThePredictions.Application\Features\Leagues\Commands\UpdateLeagueCommandHandler.cs` | 18 | "Only the league administrator can update the league." |

Also (June 4.4, second bullet): `src\ThePredictions.Application\Features\Leagues\Commands\DeleteLeagueCommandHandler.cs` line 17, currently

```csharp
        if (league.AdministratorUserId != request.DeletingUserId && !request.IsAdmin)
            throw new AuthenticationException("You are not authorised to delete this league.");
```

becomes `throw new ForbiddenAccessException("You are not authorised to delete this league.");` (remove `using System.Security.Authentication;`). Note this is a behavioural **fix**: `AuthenticationException` currently maps to nothing and surfaces as 500.

Each change needs `using ThePredictions.Application.Common.Exceptions;` added. Update the doc comment on `EnsureAdministrator` in `src\ThePredictions.Application\Services\ICurrentUserService.cs` (line 28 currently says "Throws UnauthorizedAccessException if not.") to: "Throws UnauthorizedAccessException when unauthenticated, ForbiddenAccessException when not an administrator."

Client impact of 401 -> 403: safe. `AuthorizationMessageHandler` (`src\ThePredictions.Web.Client\Authentication\AuthorizationMessageHandler.cs` line 52) only special-cases 401 (token refresh + replay); permission failures were never fixable by refresh, so removing them from the 401 path avoids pointless refresh attempts. All affected client call paths treat any non-success uniformly (they read the message).

### 5.2 Not-found standardisation (June 4.4, first bullet)

- In `GetLeagueBankDetailsQueryHandler.cs:31`, `GetLeaguePaymentInfoQueryHandler.cs:44`, `GetLeaguePayoutsQueryHandler.cs:31`, replace `throw new KeyNotFoundException($"League with ID {request.LeagueId} not found.");` with `throw new EntityNotFoundException("League", request.LeagueId);` (message becomes "League (ID: n) was not found." per the exception's own template; add `using ThePredictions.Domain.Common.Exceptions;`). Status stays 404.
- In `DeleteUserCommandHandler.cs:34`, replace the Ardalis guard `Guard.Against.NotFound(request.NewAdministratorId, newAdmin, "New Administrator User");` with the domain guard `Guard.Against.EntityNotFound(request.NewAdministratorId, newAdmin, "New Administrator User");` (already imported via `ThePredictions.Domain.Common.Guards` in that file; line 23 uses it). This fixes a live bug: the Ardalis `NotFoundException` currently surfaces as 500.

### 5.3 Raw `Exception` -> `IdentityUpdateException` (June 4.4, third bullet)

- `DeleteUserCommandHandler.cs:45`: replace `throw new Exception($"Failed to delete user: {string.Join(", ", result.Errors)}");` with `throw new IdentityUpdateException(result.Errors);`
- `UpdateUserRoleCommandHandler.cs:23`: replace `throw new Exception($"Failed to update role: {string.Join(", ", result.Errors)}");` with `throw new IdentityUpdateException(result.Errors);`
- Add `using ThePredictions.Application.Common.Exceptions;` to both. Status changes 500 -> 400 with the errors in the body; both are admin-screen operations whose pages surface the message generically. This matches the existing pattern in `RegisterCommandHandler.cs:33` and `LoginWithGoogleCommandHandler.cs:70,75,81`.

### 5.4 Update affected unit tests

Existing `tests\Unit\ThePredictions.Application.Tests.Unit` tests assert `UnauthorizedAccessException`, `KeyNotFoundException`, `AuthenticationException` or raw `Exception` for the handlers above. Before changing code, run `grep -rn "UnauthorizedAccessException\|AuthenticationException\|KeyNotFoundException\|ThrowAsync<Exception>" tests/Unit/ThePredictions.Application.Tests.Unit/` and update every affected assertion to the new exception type, keeping the `MethodName_ShouldX_WhenY` naming (e.g. `Handle_ShouldThrowForbiddenAccessException_WhenUserIsNotLeagueAdministrator`). Add new tests where a handler previously had none for the failure branch. Use `CancellationToken.None` in all calls and mock verifications.

### 5.5 PR 3 verification

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests\Unit\ThePredictions.Application.Tests.Unit
dotnet test tests\Unit\ThePredictions.API.Tests.Unit
dotnet test tests\Unit\ThePredictions.Domain.Tests.Unit
```

Domain is untouched, but run its suite anyway; if any Domain file was touched, run `tools\Test Coverage\coverage-unit.bat` and confirm 100% line and branch coverage.

---

## 6. Sequencing and deployability

| PR | Contents | Deployable alone because |
|----|----------|--------------------------|
| 1 | `ForbiddenAccessException`, `ApiErrorResponse`/`ApiErrorCodes`, middleware rewrite, `ApiErrorFactory`, AuthenticationController failure bodies, API CLAUDE.md, new API test project | All bodies keep lowercase `message`; status codes unchanged; refresh 400 contract preserved; deployed client's probes all still resolve (section 3.5) |
| 2 | Client `ApiErrorParser` + BaseFormComponent/LeagueService/SeasonPassService/AuthenticationService/BoostClientService updates; BoostsController failure body | Server already emits `ApiErrorResponse` everywhere the middleware writes; the boost server+client change is atomic within this PR |
| 3 | Handler exception sweep (403s, not-found, identity), `ICurrentUserService` doc, test updates | Client from PR 2 (and in fact the pre-existing client) handles 403/404/400 uniformly on these paths; `AuthorizationMessageHandler` stops wasting a token refresh on permission failures |

Land in that order. Do not fold PR 3 into PR 1: the status-code changes deserve their own reviewable, revertable diff.

---

## Out of scope

- Wiring server-side FluentValidation execution (June finding 1.1) - owned by the rewritten [`docs/todo/security/server-validation-gap/README.md`](../../security/server-validation-gap/README.md); this plan only provides the `ValidationException` -> 400 `VALIDATION_ERROR` mapping it will emit through.
- Swagger error examples and `[SwaggerResponse(..., typeof(ApiErrorResponse))]` attribute sweeps - remain in the [api-documentation plan](../api-documentation/README.md) Tasks 2.2 and 2.3.
- Adopting RFC 7807 `ProblemDetails` (decided against, section 2.1).
- Changing any **success** response shapes, including the anonymous `Ok(new { message = ... })` bodies on confirm-email/resend-confirmation/forgot-password and the `SuccessfulAuthenticationResponse` family.
- Changing the refresh endpoint's 400 status to 401 (the client accepts both as end-of-session, but the churn buys nothing).
- Making somewhere actually throw `EmailNotConfirmedException` (currently thrown nowhere; the mapping is kept ready, but introducing the throw is a product decision).
- Rate-limit (429) response bodies produced by the framework's rate limiter, and responses written by `[ApiKeyAuth]` or the authentication handlers before the pipeline reaches the middleware.
- The other June audit items (sections 1-3, 4.1-4.3, 4.5-4.9 of the findings document).
- Database changes (none), Brevo templates (none).

## Verification checklist

- [ ] `src\ThePredictions.Contracts\Common\ApiErrorResponse.cs` and `ApiErrorCodes.cs` exist, one public type per file, XML-doc'd, UK English
- [ ] `src\ThePredictions.Application\Common\Exceptions\ForbiddenAccessException.cs` exists in namespace `ThePredictions.Application.Common.Exceptions`
- [ ] `ErrorHandlingMiddleware` has no anonymous-object responses, no `ex.Message.Contains("A task was canceled")`, catches `OperationCanceledException` by type, guards `Response.HasStarted`, and stamps `TraceId` from `context.Items["CorrelationId"]`
- [ ] 402 body carries `extensions.seasonId`; 403 distinguishes `FORBIDDEN` vs `EMAIL_NOT_CONFIRMED` by `code`
- [ ] AuthenticationController failure paths return `ApiErrorResponse`; refresh endpoint still returns **400** on both failure paths (session-over contract intact, `ApiAuthenticationStateProvider.cs:196` unchanged)
- [ ] BoostsController apply failure returns `ApiErrorResponse` and `BoostClientService` parses it (same PR)
- [ ] Client: `ApiErrorParser` exists; `BaseFormComponent.razor`, `LeagueService.cs` (5 sites), `SeasonPassService.cs`, `AuthenticationService.cs` no longer probe `JsonNode`/ad-hoc shapes; `using System.Text.Json.Nodes;` removed where unused
- [ ] Handler sweep: all 13 permission throw sites use `ForbiddenAccessException`; `DeleteLeagueCommandHandler` no longer uses `AuthenticationException`; 3 league query handlers use `EntityNotFoundException`; `DeleteUserCommandHandler` uses `Guard.Against.EntityNotFound` (not Ardalis `NotFound`) and `IdentityUpdateException`; `UpdateUserRoleCommandHandler` uses `IdentityUpdateException`
- [ ] `src\ThePredictions.API\CLAUDE.md` Error Handling table and handler examples match section 3.6
- [ ] `ICurrentUserService.EnsureAdministrator` doc comment updated
- [ ] New project `tests\Unit\ThePredictions.API.Tests.Unit` added to `ThePredictions.sln`; `InternalsVisibleTo` added to the API csproj; every middleware mapping has a passing `InvokeAsync_ShouldX_WhenY` test; client `ApiErrorParserTests` pass; all tests use `CancellationToken.None` (xUnit1051 clean)
- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` succeeds after each PR
- [ ] `dotnet test tests\Unit\ThePredictions.API.Tests.Unit`, `dotnet test tests\Unit\ThePredictions.Application.Tests.Unit`, `dotnet test tests\Unit\ThePredictions.Web.Client.Tests.Unit` all pass
- [ ] If any Domain file was touched (it should not be): `tools\Test Coverage\coverage-unit.bat` reports 100% line and branch
- [ ] No em dashes or en dashes introduced anywhere; UK English throughout
