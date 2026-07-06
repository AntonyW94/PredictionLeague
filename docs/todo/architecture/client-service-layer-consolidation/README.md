# Client Service Layer Consolidation

## Status

**Not Started** | In Progress | Complete

## Priority

**High** - the client currently contains silent write failures (member approvals and league saves that report success when the server rejected them), plus two coexisting client architectures that make every new page a coin-flip between patterns. Phase 1 fixes real user-facing bugs; the later phases stop the duplication from growing.

## Problem statement

Two client architectures coexist in `src\ThePredictions.Web.Client`:

1. **Service layer** (the documented pattern, see `src\ThePredictions.Web.Client\CLAUDE.md` "State Management"): components inject a state service, the service holds state and raises `OnStateChange`, components subscribe in `OnInitializedAsync` and unsubscribe in `Dispose`. Dashboard flows follow this.
2. **Direct `HttpClient` injection**: 28 `.razor` files inject `HttpClient` and hand-roll URLs, deserialisation, and error handling in `@code` blocks.

This plan standardises on the service layer, fixes the silent-failure bugs on the way, and extracts the copy-pasted HTTP boilerplate into one small helper.

### Current service layer (keep and extend)

Registered in `src\ThePredictions.Web.Client\DependencyInjection.cs` (all `AddScoped`). Contents of `src\ThePredictions.Web.Client\Services\`:

| Folder | Types |
|---|---|
| `Boosts` | `BoostClientService` (no interface) |
| `Browser` | `IBrowserService`, `BrowserService` |
| `Consent` | `IConsentBannerService`, `ConsentBannerService`, `CookieConsentDecision`, `CookieConsentRecord` |
| `Dashboard` | `IDashboardStateService`, `DashboardStateService`, `DashboardPrompt` |
| `EmailSettings` | `IEmailSettingsService`, `EmailSettingsService` |
| `Leagues` | `ILeagueService`, `LeagueService`, `LeagueDashboardStateService` (no interface) |
| `Onboarding` | `IOnboardingService`, `OnboardingService` |
| `Payouts` | `IPayoutService`, `PayoutService` |
| `PricingSettings` | `IPricingSettingsService`, `PricingSettingsService`, `IServiceFeeService`, `ServiceFeeService` |
| `RunningCosts` | `IRunningCostService`, `RunningCostService` |
| `SeasonPasses` | `ISeasonPassService`, `SeasonPassService` |
| `Theme` | `IThemeService`, `ThemeService` |

Authentication lives separately in `src\ThePredictions.Web.Client\Authentication\` (`IAuthenticationService`, `AuthenticationService`).

The `HttpClient` itself is the named "API" client registered in `src\ThePredictions.Web.Client\Program.cs` (line 19); services receive it by constructor injection, e.g. `public class LeagueService(HttpClient httpClient) : ILeagueService`.

### The 28 components that inject HttpClient directly

Authoritative list (re-generate with `grep -rln "@inject HttpClient" src/ThePredictions.Web.Client`), grouped by feature area. All paths relative to `src\ThePredictions.Web.Client\`:

**Leagues (8):**
- `Components\Pages\Leagues\Create.razor`
- `Components\Pages\Leagues\Edit.razor`
- `Components\Pages\Leagues\List.razor`
- `Components\Pages\Leagues\Members.razor`
- `Components\Pages\Leagues\PrizePreview.razor`
- `Components\Pages\Leagues\Prizes.razor`
- `Components\Leagues\LeagueBoostSelector.razor`
- `Components\Leagues\PrizeSchemeEditor.razor`

**Dashboard (1):**
- `Components\Pages\Dashboard\MyLeaguesTile.razor`

**Predictions (1):**
- `Components\Pages\Predictions\Predictions.razor`

**Account / Authentication (2):**
- `Components\Pages\Account\Details.razor`
- `Components\Pages\Authentication\ConfirmEmail.razor`

**Misc (1):**
- `Components\Pages\Home.razor`

**Admin (15):**
- `Components\Pages\Admin\Competitions\Create.razor`, `Edit.razor`, `List.razor`
- `Components\Pages\Admin\EmailTests\Index.razor`
- `Components\Pages\Admin\Rounds\Create.razor`, `Edit.razor`, `List.razor`
- `Components\Pages\Admin\Seasons\Create.razor`, `Edit.razor`, `List.razor`, `SeasonPricingSection.razor`
- `Components\Pages\Admin\Teams\Create.razor`, `Edit.razor`, `List.razor`
- `Components\Pages\Admin\Users\List.razor`

## Related plans (being written in parallel - check before starting)

- `docs\todo\architecture\error-contract-standardisation\README.md` - standardises API error responses into an `ApiErrorResponse` record (`Code` / `Message` / `Errors` / `TraceId`) in `ThePredictions.Contracts\Common`. Today the API returns anonymous `{ message = ... }` objects (see `src\ThePredictions.API\Middleware\ErrorHandlingMiddleware.cs`, e.g. line 19: `await HandleKnownExceptionAsync(context, HttpStatusCode.NotFound, new { message = ex.Message });`). The helper in Phase 1 is designed to parse **both** shapes so this plan does not block on that one.
- `docs\todo\architecture\transaction-context-hardening\README.md` - hardens `src\ThePredictions.Infrastructure\Data\DbTransactionContext.cs` and `src\ThePredictions.Application\Common\Behaviours\TransactionBehaviour.cs`. Phase 2's composite command needs nested `ITransactionalRequest` sends to work; see the re-entrancy note in Phase 2 step 3 for the minimal guard if that plan has not landed yet.

## The bugs this plan fixes (verified quotes)

### Bug 1: `LeagueService.UpdateMemberStatusAsync` never checks the response

`src\ThePredictions.Web.Client\Services\Leagues\LeagueService.cs` lines 181-184:

```csharp
public async Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus newStatus)
{
    await httpClient.PostAsJsonAsync($"api/leagues/{leagueId}/members/{userId}/status", newStatus);
}
```

`PostAsJsonAsync` does not throw on 4xx/5xx, so a rejected approve/reject looks like success.

### Bug 2: `DashboardStateService.ApproveMemberAsync` / `RejectMemberAsync` only catch exceptions

`src\ThePredictions.Web.Client\Services\Dashboard\DashboardStateService.cs` lines 346-378 (Approve shown; Reject is identical with `Rejected` / "Could not reject member."):

```csharp
public async Task ApproveMemberAsync(int leagueId, string userId)
{
    PendingMembersErrorMessage = null;
    NotifyStateChanged();

    try
    {
        await leagueService.UpdateMemberStatusAsync(leagueId, userId, LeagueMemberStatus.Approved);
        await LoadPendingMembersAsync();
    }
    catch
    {
        PendingMembersErrorMessage = "Could not approve member.";
        NotifyStateChanged();
    }
}
```

Because Bug 1 swallows non-success status codes, the `catch` never fires for a 4xx/5xx: the pending list simply reloads and the member silently stays pending.

**The model to copy:** `DashboardStateService.SetLeagueArchivedAsync` (lines 283-320 of the same file) already does this correctly - it deconstructs `(success, errorMessage)` from `leagueService.SetLeagueArchivedAsync(...)`, applies an optimistic update first, and rolls back with the error message on failure. The approve/reject fixes below follow that shape.

### Bug 3: `Edit.razor` three-step league save ignores the second and third responses

`src\ThePredictions.Web.Client\Components\Pages\Leagues\Edit.razor` lines 265-283:

```csharp
private async Task<HttpResponseMessage> HandleUpdateLeagueAsync()
{
    var response = await Http.PutAsJsonAsync($"api/leagues/{LeagueId}/update", _model);
    if (!response.IsSuccessStatusCode)
        return response;

    // Set the prize scheme / boosts once, only on a schemeless league.
    if (_league is { HasPrizeScheme: false })
    {
        if ((_model!.Price > 0 || (_model.PrizeFundOverride ?? 0) > 0) && _scheme.Categories.Any())
            await Http.PutAsJsonAsync($"api/leagues/{LeagueId}/prize-scheme", _scheme);

        var enabledBoosts = _boostSelections.Where(b => b.IsEnabled).ToList();
        if (enabledBoosts.Any())
            await Http.PutAsJsonAsync($"api/boosts/league/{LeagueId}/rules", new SetLeagueBoostRulesRequest { Selections = enabledBoosts });
    }

    return response;
}
```

If step 2 or 3 fails (both are write-once endpoints that 400 when already set, 403 when not permitted), the user still sees "League updated successfully!". Worse, the league is left half-configured (settings saved, prizes/boosts not). The **primary fix is server-side**: one atomic command covering all three updates (Phase 2). The same defect exists in `Components\Pages\Leagues\Create.razor` line 203, where the boost-rules `PUT` after `POST api/leagues/create` is also unawaited-for-status.

### The copy-paste boilerplate

`LeagueService.cs` repeats the identical `(bool Success, string? ErrorMessage)` + `ReadFromJsonAsync<JsonNode>()["message"]` + `catch` block **5 times**: `JoinPublicLeagueAsync` (97-113), `JoinPrivateLeagueAsync` (147-168), `CancelJoinRequestAsync` (186-202), `DismissAlertAsync` (204-220), `SetLeagueArchivedAsync` (222-239). Representative instance (lines 97-113):

```csharp
public async Task<(bool Success, string? ErrorMessage)> JoinPublicLeagueAsync(int leagueId)
{
    var response = await httpClient.PostAsync($"api/leagues/{leagueId}/join", null);
    if (response.IsSuccessStatusCode)
        return (true, null);

    try
    {
        var errorContent = await response.Content.ReadFromJsonAsync<JsonNode>();
        var errorMessage = errorContent?["message"]?.ToString() ?? "An unknown error occurred while trying to join the league.";
        return (false, errorMessage);
    }
    catch
    {
        return (false, "An unexpected error occurred.");
    }
}
```

`DashboardStateService.cs` and `LeagueDashboardStateService.cs` repeat the `IsLoading = true` / clear error / `NotifyStateChanged()` / `try` / `catch` / `finally { IsLoading = false; NotifyStateChanged(); }` frame **10 times**: `DashboardStateService.LoadMyLeaguesAsync` (44-64), `LoadAvailableLeaguesAsync` (66-94), `LoadLeaderboardsAsync` (164-184), `LoadActiveRoundsAsync` (186-207), `LoadPendingRequestsAsync` (209-228), `LoadPendingMembersAsync` (322-344); `LeagueDashboardStateService.LoadDashboardData` (41-98), `LoadRoundResults` (112-139), `LoadSeasonRecap` (141-160), `LoadLeagueRecords` (162-181).

---

## Phase 1 - shared helper and silent-failure fixes

Small, high value, no server changes. Everything in this phase lives in the client.

### Step 1.1 - create `ApiResult` and `ApiResult<T>`

One public type per file (repo rule). Create `src\ThePredictions.Web.Client\Services\Common\ApiResult.cs`:

```csharp
namespace ThePredictions.Web.Client.Services.Common;

// Positional record on purpose: existing call sites deconstruct
// "var (success, errorMessage) = await ..." and keep working unchanged.
public record ApiResult(bool Success, string? ErrorMessage)
{
    public static ApiResult Ok() => new(true, null);

    public static ApiResult Fail(string errorMessage) => new(false, errorMessage);
}
```

Create `src\ThePredictions.Web.Client\Services\Common\ApiResultOfT.cs`:

```csharp
namespace ThePredictions.Web.Client.Services.Common;

public record ApiResult<T>(bool Success, T? Value, string? ErrorMessage)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null);

    public static ApiResult<T> Fail(string errorMessage) => new(false, default, errorMessage);
}
```

### Step 1.2 - create the HTTP helper extensions

Create `src\ThePredictions.Web.Client\Services\Common\HttpClientApiExtensions.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ThePredictions.Web.Client.Services.Common;

public static class HttpClientApiExtensions
{
    private const string NetworkErrorMessage = "We couldn't reach the server. Please try again.";

    public static async Task<ApiResult> PostForResultAsync<TRequest>(this HttpClient httpClient, string requestUri, TRequest payload, string fallbackErrorMessage)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(requestUri, payload);
            return await ToResultAsync(response, fallbackErrorMessage);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Fail(NetworkErrorMessage);
        }
        catch
        {
            return ApiResult.Fail(fallbackErrorMessage);
        }
    }

    public static async Task<ApiResult> PostForResultAsync(this HttpClient httpClient, string requestUri, string fallbackErrorMessage)
    {
        try
        {
            var response = await httpClient.PostAsync(requestUri, null);
            return await ToResultAsync(response, fallbackErrorMessage);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Fail(NetworkErrorMessage);
        }
        catch
        {
            return ApiResult.Fail(fallbackErrorMessage);
        }
    }

    public static async Task<ApiResult> PutForResultAsync<TRequest>(this HttpClient httpClient, string requestUri, TRequest payload, string fallbackErrorMessage)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync(requestUri, payload);
            return await ToResultAsync(response, fallbackErrorMessage);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Fail(NetworkErrorMessage);
        }
        catch
        {
            return ApiResult.Fail(fallbackErrorMessage);
        }
    }

    public static async Task<ApiResult> PutForResultAsync(this HttpClient httpClient, string requestUri, string fallbackErrorMessage)
    {
        try
        {
            var response = await httpClient.PutAsync(requestUri, null);
            return await ToResultAsync(response, fallbackErrorMessage);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Fail(NetworkErrorMessage);
        }
        catch
        {
            return ApiResult.Fail(fallbackErrorMessage);
        }
    }

    public static async Task<ApiResult> DeleteForResultAsync(this HttpClient httpClient, string requestUri, string fallbackErrorMessage)
    {
        try
        {
            var response = await httpClient.DeleteAsync(requestUri);
            return await ToResultAsync(response, fallbackErrorMessage);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Fail(NetworkErrorMessage);
        }
        catch
        {
            return ApiResult.Fail(fallbackErrorMessage);
        }
    }

    public static async Task<ApiResult<T>> GetForResultAsync<T>(this HttpClient httpClient, string requestUri, string fallbackErrorMessage)
    {
        try
        {
            var response = await httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
                return ApiResult<T>.Fail(await response.ReadApiErrorMessageAsync(fallbackErrorMessage));

            var value = await response.Content.ReadFromJsonAsync<T>();
            return value is null
                ? ApiResult<T>.Fail(fallbackErrorMessage)
                : ApiResult<T>.Ok(value);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(NetworkErrorMessage);
        }
        catch
        {
            return ApiResult<T>.Fail(fallbackErrorMessage);
        }
    }

    // Parses the API error body. Handles both the legacy anonymous shape { "message": "..." }
    // (ErrorHandlingMiddleware today) and the standardised ApiErrorResponse shape
    // { "code": ..., "message": ..., "errors": [...], "traceId": ... } coming from
    // docs/todo/architecture/error-contract-standardisation/README.md - both serialise the
    // human-readable text under "message". Once ApiErrorResponse exists in
    // ThePredictions.Contracts\Common, switch this to ReadFromJsonAsync<ApiErrorResponse>.
    public static async Task<string> ReadApiErrorMessageAsync(this HttpResponseMessage response, string fallbackErrorMessage)
    {
        try
        {
            var node = await response.Content.ReadFromJsonAsync<JsonNode>();
            var message = node?["message"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(message) ? fallbackErrorMessage : message;
        }
        catch
        {
            return fallbackErrorMessage;
        }
    }

    private static async Task<ApiResult> ToResultAsync(HttpResponseMessage response, string fallbackErrorMessage)
    {
        if (response.IsSuccessStatusCode)
            return ApiResult.Ok();

        return ApiResult.Fail(await response.ReadApiErrorMessageAsync(fallbackErrorMessage));
    }
}
```

### Step 1.3 - create `StateServiceBase` for the load boilerplate

Create `src\ThePredictions.Web.Client\Services\Common\StateServiceBase.cs`:

```csharp
namespace ThePredictions.Web.Client.Services.Common;

// Base for state services following the documented pattern
// (src/ThePredictions.Web.Client/CLAUDE.md, "State Management"):
// state + OnStateChange; components subscribe in OnInitializedAsync
// and unsubscribe in Dispose.
public abstract class StateServiceBase
{
    public event Action? OnStateChange;

    protected void NotifyStateChanged() => OnStateChange?.Invoke();

    // Standard load frame: flag on, clear error, notify, load, flag off, notify.
    // onError runs inside the catch for callers that must also reset state on failure.
    protected async Task RunLoadAsync(Action<bool> setLoading, Action<string?> setError, string failureMessage, Func<Task> load, Action? onError = null)
    {
        setLoading(true);
        setError(null);
        NotifyStateChanged();

        try
        {
            await load();
        }
        catch
        {
            setError(failureMessage);
            onError?.Invoke();
        }
        finally
        {
            setLoading(false);
            NotifyStateChanged();
        }
    }
}
```

### Step 1.4 - refactor `LeagueService` onto the helper and fix Bug 1

In `src\ThePredictions.Web.Client\Services\Leagues\LeagueService.cs`:

1. Add `using ThePredictions.Web.Client.Services.Common;` and remove `using System.Text.Json.Nodes;` once no longer referenced.
2. Replace `UpdateMemberStatusAsync` (lines 181-184) with:

```csharp
public async Task<ApiResult> UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus newStatus)
{
    return await httpClient.PostForResultAsync($"api/leagues/{leagueId}/members/{userId}/status", newStatus, "Could not update the member's status.");
}
```

3. Replace the four `(bool Success, string? ErrorMessage)` methods with one-liners returning `ApiResult` (the record deconstructs positionally, so `var (success, errorMessage) = ...` call sites in `DashboardStateService` lines 236, 253, 271 and 307 compile unchanged):

```csharp
public async Task<ApiResult> JoinPublicLeagueAsync(int leagueId)
{
    return await httpClient.PostForResultAsync($"api/leagues/{leagueId}/join", "An unknown error occurred while trying to join the league.");
}

public async Task<ApiResult> CancelJoinRequestAsync(int leagueId)
{
    return await httpClient.DeleteForResultAsync($"api/leagues/{leagueId}/join-request", "Could not cancel request.");
}

public async Task<ApiResult> DismissAlertAsync(int leagueId)
{
    return await httpClient.PutForResultAsync($"api/leagues/{leagueId}/dismiss-alert", "Could not dismiss notification.");
}

public async Task<ApiResult> SetLeagueArchivedAsync(int leagueId, bool isArchived)
{
    var endpoint = isArchived ? "archive" : "unarchive";
    return await httpClient.PutForResultAsync($"api/leagues/{leagueId}/{endpoint}", "Could not update league.");
}
```

4. `JoinPrivateLeagueAsync` (lines 147-168) reads a body on success, so keep its shape but replace the hand-rolled error parsing with the shared reader:

```csharp
public async Task<(bool Success, string? ErrorMessage, int? LeagueId)> JoinPrivateLeagueAsync(string entryCode)
{
    var request = new JoinLeagueRequest { EntryCode = entryCode };

    try
    {
        var response = await httpClient.PostAsJsonAsync("api/leagues/join", request);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JoinLeagueResultDto>();
            return (true, null, result?.LeagueId);
        }

        return (false, await response.ReadApiErrorMessageAsync("An unknown error occurred."), null);
    }
    catch
    {
        return (false, "An unexpected error occurred.", null);
    }
}
```

5. Update `src\ThePredictions.Web.Client\Services\Leagues\ILeagueService.cs` to match: the four `(bool Success, string? ErrorMessage)` signatures become `Task<ApiResult>`, and `UpdateMemberStatusAsync` becomes `Task<ApiResult> UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus newStatus);`. Add `using ThePredictions.Web.Client.Services.Common;`.
6. Grep for other call sites of the changed methods (`grep -rn "UpdateMemberStatusAsync\|JoinPublicLeagueAsync\|CancelJoinRequestAsync\|DismissAlertAsync\|SetLeagueArchivedAsync" src/ThePredictions.Web.Client`) and fix any deconstruction that no longer compiles. As of writing, all call sites are in `DashboardStateService` and already deconstruct `(success, errorMessage)`.

### Step 1.5 - fix Bug 2 in `DashboardStateService`

Replace `ApproveMemberAsync` and `RejectMemberAsync` (lines 346-378) with a checked version modelled on `SetLeagueArchivedAsync`:

```csharp
public async Task ApproveMemberAsync(int leagueId, string userId)
{
    await UpdateMemberStatusAsync(leagueId, userId, LeagueMemberStatus.Approved, "Could not approve member.");
}

public async Task RejectMemberAsync(int leagueId, string userId)
{
    await UpdateMemberStatusAsync(leagueId, userId, LeagueMemberStatus.Rejected, "Could not reject member.");
}

private async Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus newStatus, string failureMessage)
{
    PendingMembersErrorMessage = null;
    NotifyStateChanged();

    var (success, errorMessage) = await leagueService.UpdateMemberStatusAsync(leagueId, userId, newStatus);
    if (success)
    {
        await LoadPendingMembersAsync();
    }
    else
    {
        PendingMembersErrorMessage = errorMessage ?? failureMessage;
        NotifyStateChanged();
    }
}
```

### Step 1.6 - move both state services onto `StateServiceBase`

1. `DashboardStateService`: declare `public class DashboardStateService(...) : StateServiceBase, IDashboardStateService`. Delete its own `public event Action? OnStateChange;` (line 42) and `private void NotifyStateChanged() => OnStateChange?.Invoke();` (line 380) - both now come from the base. Convert the six framed load methods to `RunLoadAsync`. Example for `LoadMyLeaguesAsync`:

```csharp
public Task LoadMyLeaguesAsync() =>
    RunLoadAsync(
        v => IsMyLeaguesLoading = v,
        v => MyLeaguesErrorMessage = v,
        "Could not load your leagues.",
        async () => MyLeagues = await leagueService.GetMyLeaguesAsync());
```

   Convert likewise: `LoadAvailableLeaguesAsync` (pass `onError: () => { AvailableLeagues = []; HasAvailablePrivateLeagues = false; }`), `LoadLeaderboardsAsync`, `LoadActiveRoundsAsync` (clear `ActiveRoundsSuccessMessage = null;` before calling `RunLoadAsync`), `LoadPendingRequestsAsync`, `LoadPendingMembersAsync`. Leave `LoadAvailableSeasonPassesAsync` and `LoadOnboardingAsync` as they are (they have no loading flag or error message; do not invent them).

2. `LeagueDashboardStateService`: same treatment for `LoadDashboardData`, `LoadRoundResults`, `LoadSeasonRecap`, `LoadLeagueRecords` (delete its local event and `NotifyStateChanged`, inherit `StateServiceBase`). `LoadRoundResults` sets `SelectedRoundId = roundId;` before the frame - keep that line before the `RunLoadAsync` call.
3. Behaviour must not change: same flags, same error strings, same notification points.

### Step 1.7 - unit tests

Test project: `tests\Unit\ThePredictions.Web.Client.Tests.Unit` (already exists; `TestDoubles\StubHttpMessageHandler.cs` provides a stubbed `HttpMessageHandler` - read it first and reuse it). Add:

- `Services\Common\HttpClientApiExtensionsTests.cs`: success returns `Ok`; 400 with `{"message":"Boom"}` returns `Fail("Boom")`; 400 with a non-JSON body returns the fallback; network exception returns the "couldn't reach the server" message; `GetForResultAsync<T>` deserialises on 200 and fails on 404.
- `Services\Leagues\LeagueServiceTests.cs`: `UpdateMemberStatusAsync` returns failure (not success) when the stub responds 403.
- `Services\Dashboard\DashboardStateServiceTests.cs`: `ApproveMemberAsync` sets `PendingMembersErrorMessage` and raises `OnStateChange` when the league service returns `ApiResult.Fail(...)`; clears it and reloads on success. Test naming convention: `MethodName_ShouldX_WhenY()`; use `CancellationToken.None`, never a bare `default`.

### Step 1.8 - verify

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests\Unit\ThePredictions.Web.Client.Tests.Unit
```

---

## Phase 2 - atomic league save (server-side fix for Bug 3)

The three endpoints `Edit.razor` calls are (verified):

| Call | Controller action | Command |
|---|---|---|
| `PUT api/leagues/{leagueId}/update` | `LeaguesController.UpdateLeagueAsync` (`src\ThePredictions.API\Controllers\LeaguesController.cs` lines 400-433) | `UpdateLeagueCommand` (`: IRequest`, **not** transactional) |
| `PUT api/leagues/{leagueId}/prize-scheme` | `LeaguesController.SetPrizeSchemeAsync` (lines 632-649) | `SetPrizeSchemeCommand` (`: IRequest, ITransactionalRequest`) |
| `PUT api/boosts/league/{leagueId}/rules` | `BoostsController.SetLeagueBoostRulesAsync` (`src\ThePredictions.API\Controllers\BoostsController.cs` lines 80-97) | `SetLeagueBoostRulesCommand` (`: IRequest, ITransactionalRequest`) |

### Step 2.1 - extend the contract

In `src\ThePredictions.Contracts\Leagues\UpdateLeagueRequest.cs` add two optional properties (mirrors `CreateLeagueRequest`, which already carries a `PrizeScheme`):

```csharp
// Optional one-shot prize scheme, saved atomically with the settings update.
// Only honoured while the league has no scheme (write-once, same as the standalone endpoint).
public PrizeSchemeRequest? PrizeScheme { get; set; }

// Optional one-shot boost selection, saved atomically with the settings update.
public List<LeagueBoostSelectionDto>? BoostSelections { get; set; }
```

Add the required `using ThePredictions.Contracts.Prizes;` / `using ThePredictions.Contracts.Boosts;`. Extend `src\ThePredictions.Validators\Leagues\UpdateLeagueRequestValidator.cs`:

```csharp
RuleFor(x => x.PrizeScheme!)
    .SetValidator(new PrizeSchemeRequestValidator())
    .When(x => x.PrizeScheme is not null);
```

### Step 2.2 - composite command

Create `src\ThePredictions.Application\Features\Leagues\Commands\SaveLeagueSettingsCommand.cs`:

```csharp
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record SaveLeagueSettingsCommand(
    UpdateLeagueCommand Update,
    PrizeSchemeRequest? PrizeScheme,
    List<LeagueBoostSelectionDto>? BoostSelections) : IRequest, ITransactionalRequest;
```

Create `src\ThePredictions.Application\Features\Leagues\Commands\SaveLeagueSettingsCommandHandler.cs`:

```csharp
using MediatR;
using ThePredictions.Application.Features.Boosts.Commands;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class SaveLeagueSettingsCommandHandler(IMediator mediator) : IRequestHandler<SaveLeagueSettingsCommand>
{
    public async Task Handle(SaveLeagueSettingsCommand request, CancellationToken cancellationToken)
    {
        await mediator.Send(request.Update, cancellationToken);

        if (request.PrizeScheme is not null && request.PrizeScheme.Categories.Count != 0)
            await mediator.Send(new SetPrizeSchemeCommand(request.Update.Id, request.Update.UserId, request.PrizeScheme), cancellationToken);

        if (request.BoostSelections is { Count: > 0 })
            await mediator.Send(new SetLeagueBoostRulesCommand(request.Update.Id, request.Update.UserId, request.BoostSelections), cancellationToken);
    }
}
```

All existing authorisation, write-once, and free-league guards in `SetPrizeSchemeCommandHandler` / `SetLeagueBoostRulesCommandHandler` still run; nothing is duplicated.

### Step 2.3 - transaction re-entrancy (BLOCKING precondition)

`SetPrizeSchemeCommand` and `SetLeagueBoostRulesCommand` are themselves `ITransactionalRequest`, so sending them from inside the composite's transaction hits `DbTransactionContext.BeginAsync` (`src\ThePredictions.Infrastructure\Data\DbTransactionContext.cs` lines 49-56), which currently throws:

```csharp
public Task BeginAsync(CancellationToken cancellationToken)
{
    if (_begun)
        throw new InvalidOperationException("A transaction is already active.");

    _begun = true;
    return Task.CompletedTask;
}
```

This is in scope for `docs\todo\architecture\transaction-context-hardening\README.md`. **If that plan has landed, use whatever nesting mechanism it introduced.** If not, apply this minimal guard in `src\ThePredictions.Application\Common\Behaviours\TransactionBehaviour.cs` so only the outermost transactional request begins/commits:

```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
{
    var requestName = typeof(TRequest).Name;

    // A transactional request sent from inside another transactional request
    // joins the ambient transaction instead of trying to open a second one.
    if (transactionContext.HasActiveTransaction)
    {
        logger.LogDebug("Joining ambient transaction for {RequestName}", requestName);
        return await next(cancellationToken);
    }

    await transactionContext.BeginAsync(cancellationToken);

    try
    {
        logger.LogDebug("Beginning transaction for {RequestName}", requestName);

        var response = await next(cancellationToken);

        await transactionContext.CommitAsync(cancellationToken);

        logger.LogDebug("Committed transaction for {RequestName}", requestName);

        return response;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Transaction for {RequestName} failed. Rolling back.", requestName);
        throw;
    }
}
```

Note the caveat either way: `UpdateLeagueCommandHandler` sends `NotifyMemberOfLeagueApprovalCommand` (emails) at the end of its handler (`UpdateLeagueCommandHandler.cs` lines 54-57); inside the composite these now run before the commit. Acceptable for this change; record it in the PR description.

### Step 2.4 - controller

In `LeaguesController.UpdateLeagueAsync` (lines 409-433), wrap the existing `UpdateLeagueCommand` construction in the composite and send that instead:

```csharp
var command = new SaveLeagueSettingsCommand(
    new UpdateLeagueCommand(
        leagueId,
        request.Name,
        request.Price,
        request.EntryDeadlineUtc,
        request.PointsForExactScore,
        request.PointsForCorrectResult,
        CurrentUserId,
        request.BankAccountName,
        request.BankSortCode,
        request.BankAccountNumber,
        request.PaymentReferenceTemplate,
        request.PrizeFundOverride,
        RequiresMemberApproval: request.RequiresMemberApproval,
        IsListed: request.IsListed,
        LeagueUrlBase: Request.Headers["Origin"].ToString()),
    request.PrizeScheme,
    request.BoostSelections);

await mediator.Send(command, cancellationToken);

return NoContent();
```

**Pre-existing bug found while writing this plan:** the current controller call (lines 414-428) never passes `request.PrizeFundOverride` into the command - `PrizeFundOverride` is positional parameter 12 of `UpdateLeagueCommand` (`src\ThePredictions.Application\Features\Leagues\Commands\UpdateLeagueCommand.cs` line 17, default `null`) and the controller skips it by jumping to the named arguments `RequiresMemberApproval` / `IsListed` / `LeagueUrlBase`. `UpdateLeagueCommandHandler` line 45 then runs `league.SetPrizeFundOverride(request.PrizeFundOverride)` with `null` on every update, even though `Edit.razor` binds `_model.PrizeFundOverride` and `UpdateLeagueRequest` carries it (line 19). The snippet above maps it explicitly - keep that, verify the positional slot against the record when editing, and call the fix out in the PR description.

### Step 2.5 - client

`Edit.razor` `HandleUpdateLeagueAsync` becomes a single call:

```csharp
private async Task<HttpResponseMessage> HandleUpdateLeagueAsync()
{
    // Prizes/boosts are write-once: only send them while the league has no scheme yet.
    if (_league is { HasPrizeScheme: false })
    {
        _model!.PrizeScheme = (_model.Price > 0 || (_model.PrizeFundOverride ?? 0) > 0) && _scheme.Categories.Any()
            ? _scheme
            : null;

        var enabledBoosts = _boostSelections.Where(b => b.IsEnabled).ToList();
        _model.BoostSelections = enabledBoosts.Any() ? enabledBoosts : null;
    }

    return await Http.PutAsJsonAsync($"api/leagues/{LeagueId}/update", _model);
}
```

One request, one response, and `BaseFormComponent` (whose `SubmitAction` is `Func<Task<HttpResponseMessage>>`, `Components\Shared\BaseFormComponent.razor` line 26) now reports failure correctly for the whole save.

### Step 2.6 - same fix for `Create.razor`

`Components\Pages\Leagues\Create.razor` line 203 fires `PUT api/boosts/league/{created.Id}/rules` after `POST api/leagues/create` and ignores the response. Mirror the pattern: add `List<LeagueBoostSelectionDto>? BoostSelections` to `CreateLeagueRequest` (`src\ThePredictions.Contracts\Leagues\CreateLeagueRequest.cs` - it already carries `PrizeScheme`), pass it through `CreateLeagueCommand` (already `ITransactionalRequest`), and have `CreateLeagueCommandHandler` send `SetLeagueBoostRulesCommand` when selections are present (read the handler first and slot the send in after the league is persisted). Then delete the follow-up `PUT` from `Create.razor`.

### Step 2.7 - tests and verify

- Application tests in `tests\Unit\ThePredictions.Application.Tests.Unit`: composite handler sends update always, scheme/boosts only when present; `TransactionBehaviour` joins an ambient transaction instead of throwing (if the guard was added here).
- No database schema changes in this phase (no migration, no `database-schema.md` update needed).

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests\Unit\ThePredictions.Application.Tests.Unit
dotnet test tests\Unit\ThePredictions.Web.Client.Tests.Unit
dotnet test tests\Unit\ThePredictions.Validators.Tests.Unit
```

---

## Phase 3 - migrate components to services, feature by feature

Rules for every batch:

- Move URL construction, (de)serialisation, and error parsing into the feature's service. Components keep only UI state and call service methods.
- Reads return the DTO (or `null`/`[]`), writes return `ApiResult` via the Phase 1 helper.
- **Exception:** pages built on `BaseFormComponent` need `Func<Task<HttpResponseMessage>>` for `SubmitAction`. For those submit paths the service method returns `Task<HttpResponseMessage>` (still centralising the URL and payload); converting `BaseFormComponent` to `ApiResult` is out of scope.
- New services follow the existing pattern exactly: interface + implementation in `Services\<Feature>\`, primary constructor `(HttpClient httpClient)`, registered `AddScoped<IXxx, Xxx>()` in `DependencyInjection.cs`.
- State-holding services (if any new ones are needed) extend `StateServiceBase` and follow the `CLAUDE.md` component pattern (subscribe `OnStateChange += StateHasChanged` in `OnInitializedAsync`, unsubscribe in `Dispose`).
- After each batch: `grep -rn "@inject HttpClient" src/ThePredictions.Web.Client` must show only the not-yet-migrated batches.

### Batch 3a - Leagues (8 components)

Extend `ILeagueService`/`LeagueService` with the missing endpoints (all verified against the components):

| New method | Endpoint | Used by |
|---|---|---|
| `Task<CreateLeaguePageData?> GetCreateDataAsync()` | `GET api/leagues/create-data` | `Create.razor:137` |
| `Task<HttpResponseMessage> CreateLeagueAsync(CreateLeagueRequest request)` | `POST api/leagues/create` | `Create.razor:193` (BaseFormComponent path; the response body `LeagueDto` is read by the caller) |
| `Task<LeagueDto?> GetLeagueAsync(int leagueId)` | `GET api/leagues/{leagueId}` | `Edit.razor:227` |
| `Task<LeagueBankDetailsDto?> GetBankDetailsAsync(int leagueId)` | `GET api/leagues/{leagueId}/bank-details` | `Edit.razor:253` |
| `Task<HttpResponseMessage> UpdateLeagueAsync(int leagueId, UpdateLeagueRequest request)` | `PUT api/leagues/{leagueId}/update` | `Edit.razor` (Phase 2 shape) |
| `Task<ManageLeaguesDto?> GetManageLeaguesAsync()` | `GET api/leagues` | `List.razor:100` |
| `Task<ApiResult> DeleteLeagueAsync(int leagueId)` | `DELETE api/leagues/{leagueId}` | `List.razor:120` |
| `Task<LeagueMembersPageDto?> GetMembersAsync(int leagueId)` | `GET api/leagues/{leagueId}/members` | `Members.razor:187` |
| `Task<LeaguePrizesPageDto?> GetPrizesPageAsync(int leagueId)` | `GET api/leagues/{leagueId}/prizes` | `Prizes.razor:195` |
| `Task<ApiResult<PrizeBreakdownDto>> EvaluateSchemeAsync(<request type used at PrizeSchemeEditor.razor:240>)` | `POST api/leagues/evaluate-scheme` | `PrizeSchemeEditor.razor:240` |

Component changes:

- **`Members.razor`** - delete its private `UpdateMemberStatusAsync` (lines 212-229), which duplicates the service method in parallel; call `LeagueService.UpdateMemberStatusAsync` and use the returned `ApiResult.ErrorMessage` for `_errorMessage`, reloading via `GetMembersAsync` on success. Note the irony to preserve: the component copy already checked `IsSuccessStatusCode` (line 218) - keep that behaviour, now via `ApiResult`.
- **`PrizePreview.razor`** - replace its direct `GET` (line 110) and join `POST`s (lines 150-151) with the existing `GetJoinPreviewByIdAsync` / `GetJoinPreviewAsync` / `JoinPublicLeagueAsync` / `JoinPrivateLeagueAsync`.
- **`LeagueBoostSelector.razor`** - add `Task<List<BoostCatalogueItemDto>> GetCatalogueAsync()` (`GET api/boosts/catalogue`, line 53) to `BoostClientService` and inject that instead of `HttpClient`.
- **`MyLeaguesTile.razor`** - remove `@inject HttpClient Http` (line 5) and its private `_breakdowns` dictionary + `LoadPrizeBreakdownsAsync` (lines 540-557), which fetches `api/leagues/{id}/prize-breakdown` directly (line 546) despite already consuming `IDashboardStateService`. Instead add to `DashboardStateService` (and `IDashboardStateService`):

```csharp
public IReadOnlyDictionary<int, PrizeBreakdownDto> PrizeBreakdowns => _prizeBreakdowns;
private readonly Dictionary<int, PrizeBreakdownDto> _prizeBreakdowns = new();

public async Task LoadPreDeadlinePrizeBreakdownsAsync()
{
    foreach (var league in MyLeagues.Where(l => !l.IsFinished && !l.IsFree && l.EntryDeadlineUtc is { } deadline && DateTime.UtcNow < deadline))
    {
        var breakdown = await leagueService.GetPrizeBreakdownAsync(league.Id);
        if (breakdown is not null && breakdown.Categories.Any(c => c.SubPot > 0))
            _prizeBreakdowns[league.Id] = breakdown;
    }

    NotifyStateChanged();
}
```

  The tile then reads `DashboardState.PrizeBreakdowns` and calls `await DashboardState.LoadPreDeadlinePrizeBreakdownsAsync();` where it called its own loader (line 533). Keep the sequential fetch behaviour as-is (see appendix).

### Batch 3b - Predictions (1 component)

Create `Services\Predictions\IPredictionService.cs` + `PredictionService.cs`:

```csharp
public interface IPredictionService
{
    Task<PredictionPageDto?> GetPredictionPageAsync(int roundId);          // GET api/predictions/{roundId}  (Predictions.razor:291)
    Task<ApiResult> SubmitPredictionsAsync(SubmitPredictionsRequest request); // POST api/predictions/submit  (Predictions.razor:498)
}
```

`Predictions.razor` drops `@inject HttpClient Http` (line 5); the boost calls already go through `BoostClientService`. On submit failure, show `ApiResult.ErrorMessage` instead of the current hard-coded "There was an error saving your predictions." (line 511) when a server message is available.

### Batch 3c - Admin (15 components)

Enumerate every remaining direct call mechanically before starting:

```
grep -rn "Http\.\(Get\|Post\|Put\|Delete\)" src/ThePredictions.Web.Client/Components/Pages/Admin
```

Create one service per admin area, same pattern as above (interface + implementation in `Services\Admin\<Area>\`, registered in `DependencyInjection.cs`). Representative sample (verified endpoints); enumerate the rest from the grep output and map 1:1:

- **`IAdminCompetitionService`**: `GetCompetitionsAsync()` (`GET api/admin/competitions`), `GetCompetitionAsync(int id)` (`GET api/admin/competitions/{id}`), `CreateCompetitionAsync(...)` (`POST api/admin/competitions/create`, returns `HttpResponseMessage` for `BaseFormComponent`), `UpdateCompetitionAsync(int id, ...)` (`PUT api/admin/competitions/{id}/update`, `HttpResponseMessage`), `DeleteCompetitionAsync(int id)` (`DELETE api/admin/competitions/{id}`, `ApiResult`).
- **`IAdminSeasonService`**: `GetSeasonsAsync()`, `GetSeasonAsync(int id)`, `CreateSeasonAsync(...)` / `UpdateSeasonAsync(int id, ...)` (`HttpResponseMessage`), `SetSeasonStatusAsync(int id, ... )` (`PUT api/admin/seasons/{id}/status`, `ApiResult`), `DeleteSeasonAsync(int id)` (`ApiResult`), `HasPredictionsAsync(int id)` (`GET .../has-predictions`), `GetTournamentMappingsAsync(int id)` (`GET .../tournament-mappings`), `LookupApiLeaguesAsync(...)` and `GetApiRoundsAsync(int apiLeagueId, int seasonYear)` (`GET api/admin/seasons/api-rounds?...`), `GetPriceRecommendationAsync(...)` (endpoint at `SeasonPricingSection.razor:79`).
- **`IAdminRoundService`**: `GetRoundAsync(int id)`, `GetRoundsBySeasonAsync(int seasonId)`, `CreateRoundAsync(...)` / `UpdateRoundAsync(int id, ...)` (`HttpResponseMessage`), `ResendDigestAsync(int roundId)` and `ResendPrizeEmailsAsync(int roundId)` (`POST`, `ApiResult`).
- **`IAdminTeamService`**: `GetTeamsAsync(int? seasonId = null)`, `GetTeamAsync(int id)`, `CreateTeamAsync(...)` / `UpdateTeamAsync(int id, ...)` (`HttpResponseMessage`).
- **`IAdminUserService`**: `GetUsersAsync()` (`GET api/admin/users`), `SetUserRoleAsync(string userId, string newRole)` (`POST api/admin/users/{id}/role`, `ApiResult`), `UserOwnsLeaguesAsync(string userId)` (`GET .../owns-leagues`), `DeleteUserAsync(string userId, ...)` (`POST .../delete`, `ApiResult`).
- **`IAdminEmailTestService`**: `GetTemplatesAsync()`, `GetDefaultsAsync(...)`, `SendTestAsync(...)` (`ApiResult`); reuse `IAdminUserService.GetUsersAsync()` for the recipient list instead of duplicating `GET api/admin/users` (currently duplicated at `EmailTests\Index.razor:180` and `Users\List.razor:348`).

Method signatures take the same request/response DTO types the components already bind; do not invent new DTOs.

### Batch 3d - Account and Authentication (2 components)

- **`Details.razor`**: create `Services\Account\IAccountService.cs` + `AccountService.cs` with `Task<UserDetails?> GetDetailsAsync()` (`GET api/account/details`, line 83) and `Task<HttpResponseMessage> UpdateDetailsAsync(...)` (`PUT api/account/details`, line 106, `BaseFormComponent` path).
- **`ConfirmEmail.razor`**: extend the existing `IAuthenticationService` (`src\ThePredictions.Web.Client\Authentication\IAuthenticationService.cs`) with `Task<ApiResult> ConfirmEmailAsync(string token)` (`POST api/authentication/confirm-email`, line 77) and `Task<ApiResult> ResendConfirmationAsync()` (`POST api/authentication/resend-confirmation`, line 94), implemented in `AuthenticationService` following its existing style.

### Batch 3e - Misc (1 component)

- **`Home.razor`**: create `Services\Homepage\IHomepageService.cs` + `HomepageService.cs` with `Task<List<HomepageSeasonDto>> GetSeasonsAsync()` (`GET api/homepage/seasons`, line 287).

### Batch verification (after each batch)

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests\Unit\ThePredictions.Web.Client.Tests.Unit
grep -rn "@inject HttpClient" src/ThePredictions.Web.Client
```

After the final batch the grep must return no matches.

---

## Appendix - fat components doing orchestration (low priority, recorded only)

Not specced here; each deserves its own small refactor once the service migration lands. Evidence:

- `Components\Pages\Dashboard\MyLeaguesTile.razor` (757 lines): carousel index/track maths in the component (lines 577-675), JS interop height sync via `blazorInterop.updateCarouselHeight` (lines 665-669), a 1-second `System.Timers.Timer` for the countdown re-render (lines 559-567), sequential per-league prize-breakdown fetches in a foreach (lines 540-557).
- `Components\Pages\Predictions\Predictions.razor` (523 lines): synthesises a "None" boost option client-side (lines 322-333), applies the mandatory-boost-on-last-round rule client-side (lines 337-342 and 388-389), sequential per-league eligibility fetches in `OnInitializedAsync` (lines 313-346), and a multi-step delete/apply boost saga inside `HandleSubmitPredictionsAsync` (lines 451-496).
- `Components\Pages\Admin\Seasons\Edit.razor` (575 lines): API-league lookup, tournament-mapping assembly and stage fetching orchestrated in the page (lines 287-448), plus delete flow (line 552).
- `Components\Pages\Admin\Users\List.razor` (453 lines): role change (line 374), owns-leagues pre-check (line 400) and delete (line 434) orchestrated inline.

## Out of scope

- Redesigning `BaseFormComponent`'s `Func<Task<HttpResponseMessage>> SubmitAction` contract (`Components\Shared\BaseFormComponent.razor` line 26). Form-submit service methods return `HttpResponseMessage` for now.
- Implementing `ApiErrorResponse` in `ThePredictions.Contracts\Common` - owned by `docs\todo\architecture\error-contract-standardisation\README.md`. This plan only consumes whichever shape is live.
- Hardening `DbTransactionContext` beyond the minimal re-entrancy guard in Phase 2 step 3 (explicit rollback, async ADO, savepoints) - owned by `docs\todo\architecture\transaction-context-hardening\README.md`.
- Decomposing the fat components listed in the appendix (carousel extraction, boost-selection state service, parallelising sequential fetches).
- Any change to authentication flows (`ApiAuthenticationStateProvider`, token refresh, `AuthorizationMessageHandler`).
- Server-side pagination, caching, or endpoint consolidation beyond the atomic league save.

## Verification checklist

- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` passes after every phase.
- [ ] `dotnet test tests\Unit\ThePredictions.Web.Client.Tests.Unit` passes, including the new `HttpClientApiExtensions`, `LeagueService`, and `DashboardStateService` tests.
- [ ] `dotnet test tests\Unit\ThePredictions.Application.Tests.Unit` and `dotnet test tests\Unit\ThePredictions.Validators.Tests.Unit` pass after Phase 2.
- [ ] Approving/rejecting a member against a failing API (stubbed 403) surfaces "Could not approve member." / the server message instead of silently reloading.
- [ ] League edit: a failing prize-scheme or boost-rules write causes the whole save to report failure (no more half-configured leagues); a plain settings edit with no scheme/boosts still succeeds.
- [ ] League create: boost selections save atomically with the league; the follow-up `PUT api/boosts/league/{id}/rules` call is gone from `Create.razor`.
- [ ] `grep -rn "@inject HttpClient" src/ThePredictions.Web.Client` returns no matches after the final Phase 3 batch (and only unmigrated batches before then).
- [ ] `grep -rn "ReadFromJsonAsync<JsonNode>" src/ThePredictions.Web.Client` returns no matches outside `HttpClientApiExtensions.cs`.
- [ ] No behavioural regressions in the state services: same loading flags, same error strings, same `OnStateChange` notification points (spot-check the dashboard, league dashboard, and pending-members flows in the running app).
- [ ] Components still subscribe to `OnStateChange` in `OnInitializedAsync` and unsubscribe in `Dispose`, per `src\ThePredictions.Web.Client\CLAUDE.md`.
- [ ] No `.sql` files created; no database schema changes made (none are needed by this plan).
