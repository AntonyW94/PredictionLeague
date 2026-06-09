# Task 3: CQRS Layer

**Parent Feature:** [Email Test Tool](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped (PR #103) - `GetEmailTestTemplatesQuery`, `GetEmailTestDefaultsQuery`, and `SendTestEmailCommand`.

## Goal

Expose the discovery service and resolver service through MediatR queries, and add one command for sending the actual test email. The Blazor page in Task 4 only talks to MediatR through the API - it never references infrastructure types directly.

## Queries

### `GetEmailTemplatesQuery`

- Input: none.
- Output: `IReadOnlyList<EmailTemplateSummaryDto>` where each item is `{ long Id, string Name, bool IsActive }`.
- Handler: calls `IBrevoTemplateService.ListTemplatesAsync`, projects to DTO.

### `GetEmailTemplateDetailsQuery(long TemplateId, Guid DataSourceUserId)`

- Output:
  ```csharp
  public record EmailTemplateDetailsDto(
      long Id,
      string Name,
      bool IsActive,
      IReadOnlyList<EmailTemplateParamDto> Parameters);

  public record EmailTemplateParamDto(string Name, string DefaultValue);
  ```
- Handler:
  1. Calls `IBrevoTemplateService.GetTemplateAsync(TemplateId)` to get param names.
  2. Loads `ApplicationUser` for `DataSourceUserId` (via `IUserManager`).
  3. Calls `ITemplateDefaultsService.GetDefaultsAsync(...)` to fill defaults.
  4. Maps to DTO. Params with no default get an empty string.

## Command

### `SendTestEmailCommand`

```csharp
public record SendTestEmailCommand(
    long TemplateId,
    IReadOnlyDictionary<string, string> Parameters)
    : IRequest<SendTestEmailResultDto>;

public record SendTestEmailResultDto(string MessageId);
```

Handler responsibilities:

1. Resolve the **calling admin's** email from `ICurrentUserService` / claims - **never** trust a recipient from the request.
2. Confirm the template is active. If not, throw `InvalidOperationException("Template is inactive in Brevo")`. The middleware turns this into a 400.
3. Build an anonymous params object from the dictionary. Since Brevo's SDK accepts `object`, we can construct it via an `ExpandoObject` so dynamic keys work:
   ```csharp
   var paramsObj = new ExpandoObject() as IDictionary<string, object?>;
   foreach (var kvp in command.Parameters)
       paramsObj[kvp.Key] = kvp.Value;
   ```
4. Call `IEmailService.SendTemplatedEmailAsync(adminEmail, templateId, paramsObj)`.
5. Return the message ID logged by the service. (This requires a small change to `IEmailService.SendTemplatedEmailAsync` to return the message ID rather than logging-only - see "Adjustments" below.)

### Adjustments to existing code

`IEmailService.SendTemplatedEmailAsync` currently returns `Task` and logs the message ID internally. To surface it to the test page:

```csharp
Task<EmailSendResult> SendTemplatedEmailAsync(string to, long templateId, object parameters);

public record EmailSendResult(string MessageId);
```

Update:
- `BrevoEmailService` to return the result.
- All existing callers (`RequestPasswordResetCommandHandler`, `NotifyLeagueAdminOfJoinRequestCommandHandler`, `SendScheduledRemindersCommandHandler`) to ignore the result (they don't need it).

## Authorisation

All three handlers must be reachable only by admins. Use whatever pattern the existing admin endpoints use (likely `[Authorize(Roles = RoleNames.Administrator)]` on the controller).

## API endpoints

Single controller `EmailTestsController` in the API project, route `/api/admin/email-tests`:

| Method | Route | Maps to |
|---|---|---|
| GET | `/api/admin/email-tests/templates` | `GetEmailTemplatesQuery` |
| GET | `/api/admin/email-tests/templates/{id}?dataSourceUserId=...` | `GetEmailTemplateDetailsQuery` |
| POST | `/api/admin/email-tests/send` | `SendTestEmailCommand` |
| POST | `/api/admin/email-tests/refresh` | Calls `IBrevoTemplateService.InvalidateCache()` |

The Refresh endpoint is plain (no MediatR) - it just touches the singleton cache.

User-list dropdown reuses an existing query - check `Features/Admin` for `GetUsersQuery` or similar; if none exists, create a minimal one returning `{ Id, FirstName, LastName, Email }`.

## Tests

In `ThePredictions.Application.Tests.Unit`:

- `GetEmailTemplatesQueryHandler_ShouldReturnAllTemplates_WhenServiceReturnsThem`
- `GetEmailTemplateDetailsQueryHandler_ShouldFillDefaults_WhenUserExists`
- `GetEmailTemplateDetailsQueryHandler_ShouldThrow_WhenUserNotFound`
- `SendTestEmailCommandHandler_ShouldSendToCallingAdmin_NeverToParameter`
- `SendTestEmailCommandHandler_ShouldThrow_WhenTemplateInactive`
- `SendTestEmailCommandHandler_ShouldReturnMessageId_OnSuccess`

The single most important test: `SendTestEmailCommandHandler_ShouldSendToCallingAdmin_NeverToParameter` - this is the safety guarantee.

## Verification

- [ ] Calling `GET /templates` from Postman with an admin JWT returns the live Brevo template list.
- [ ] Calling `GET /templates/{id}` returns realistic defaults derived from the picked user.
- [ ] `POST /send` ignores any `to` field in the body and sends to the calling admin's email only.
- [ ] Inactive templates produce a 400 with a clear message.
- [ ] Existing handlers still work after `IEmailService` returns `EmailSendResult` instead of `Task`.
