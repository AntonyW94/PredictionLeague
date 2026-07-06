# Server-Side Validation Gap

## Status

**Not Started** | In Progress | Complete

## Summary

FluentValidation validators exist for every major Contracts request DTO, but as of July 2026 they execute **nowhere**:

- **Server:** the only server-side mechanism that runs validators is the MediatR `ValidationBehaviour`, which resolves `IValidator<TRequest>` for the command/query type. Every validator in `src/ThePredictions.Validators` targets a Contracts request DTO, so `validators.Any()` is always false and every request passes straight through. No FluentValidation auto-validation is enabled, controllers never validate manually, and Contracts DTOs carry no DataAnnotations.
- **Client:** the custom `FluentValidationValidator` Blazor component (introduced in commit `7bef0c6d`, 30 January 2026) resolves `IValidator<model type>` from DI and **silently no-ops when none is registered**. Nothing in `src/ThePredictions.Web.Client` registers any validator, so every form using `<FluentValidationValidator />` submits unconditionally.

This plan revives client-side validation (Phase 1) and enforces validation server-side at the API boundary (Phase 2), per the binding June 2026 audit decision.

## Priority

**High** - security-relevant, and the January 2026 mitigations were partly illusory (see History).

## Severity

**Medium** - P2. Reassessed July 2026 and kept at Medium because domain guards, model binding type coercion and database constraints still stand; the priority is High because two of the four mitigations recorded in January turned out not to hold.

## CWE Reference

CWE-20 (Improper Input Validation)

## OWASP Reference

A03:2021 - Injection (related)

## History

Never silently erase old decisions. The record:

| Date | Event |
|------|-------|
| **January 2026** | Deferred as accepted risk. Rationale: the recommended fix (`FluentValidation.AspNetCore` auto-validation) was deprecated upstream; creating ~50 command/query validators duplicated the request-DTO validators; and four mitigations were believed to hold: client-side validation, domain guards, database constraints, model binding. |
| **30 January 2026** | Commit `7bef0c6d` ("Replace Blazored.FluentValidation with custom implementation") replaced Blazored.FluentValidation's assembly-scanning validator discovery with DI resolution in `FluentValidationValidator.cs`. The DI registrations were never added to the Web.Client container, so client-side validation died on that day without anyone noticing - the component silently no-ops when no validator resolves. |
| **June 2026** | Full-codebase audit ([`docs/todo/architecture/code-consistency-audit/2026-06-code-review-findings.md`](../../architecture/code-consistency-audit/2026-06-code-review-findings.md), item 1.1, the headline finding) reversed the deferral. **Decision (binding, agreed with the product owner 2026-06-12): "gap - fix it. The server must enforce the same rules as the browser."** |
| **July 2026** | Review discovered the client-side situation above: the January deferral's key mitigation ("client-side validation works") had been false since 30 January. Validation currently executes nowhere. This document was rewritten as an active plan. |

### The January "deprecated auto-validation" objection, answered

The January deferral leaned on FluentValidation deprecating `AddFluentValidationAutoValidation()`. The upstream reasons were: ASP.NET Core's MVC validation pipeline is synchronous (so async rules cannot run), the maintenance burden of tracking framework changes, and auto-validation hiding where validators execute. None of those reasons applies to **owning a ~30-line action filter ourselves**: our filter calls `ValidateAsync` (fully asynchronous), it is a small explicit component in our own codebase with our own tests, and its registration is one visible line in `DependencyInjection.cs`. We are not resurrecting the deprecated package; we are writing the small piece of it we need, which is exactly what the FluentValidation maintainers recommend for people who still want boundary validation. The objection is answered on the record.

## Verified current state (July 2026)

All claims below were re-verified against the code:

- `src/ThePredictions.API/DependencyInjection.cs` line 213 registers the validators (`services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();`, default lifetime **Scoped**) and line 223 adds `cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));` to MediatR.
- `src/ThePredictions.Application/Common/Behaviours/ValidationBehaviour.cs` line 11: `if (!validators.Any()) return await next(cancellationToken);` - always taken, because no validator targets a command or query type.
- `src/ThePredictions.Web.Client/Components/Shared/FluentValidationValidator.cs` lines 38-42: `GetValidatorForModel` returns `ServiceProvider.GetService(validatorType) as IValidator`, and every handler guards `if (... _validator is null) return;` - the silent no-op.
- `src/ThePredictions.Web.Client/Program.cs` and `src/ThePredictions.Web.Client/DependencyInjection.cs`: no `AddValidators*` call anywhere.
- `src/ThePredictions.Web.Client/ThePredictions.Web.Client.csproj` already references the Validators project (with `TreatAsUsed`), so the validator types ship to the browser today - they are just never registered.
- `src/ThePredictions.API/Middleware/ErrorHandlingMiddleware.cs` lines 41-45 already catch `FluentValidation.ValidationException` and return 400 with `{ errors = ex.Errors }`.

Forms affected by the dead client-side validation (`<FluentValidationValidator />` appears in):

| File | Form model |
|------|-----------|
| `Components/Shared/BaseFormComponent.razor` | generic `TModel` (used by Account Details, Leagues Create/Edit, Admin Competitions/Rounds/Seasons/Teams Create/Edit) |
| `Components/Pages/Authentication/Login.razor` | `LoginRequest` |
| `Components/Pages/Authentication/Register.razor` | `RegisterRequest` |
| `Components/Pages/Dashboard/JoinPrivateLeagueModal.razor` | `JoinLeagueRequest` |
| `Components/Pages/Account/PayoutDetails.razor` | `SetPayoutDetailsRequest` |
| `Components/Pages/Predictions/Predictions.razor` | `SubmitPredictionsRequest` |
| `Components/Pages/Admin/RunningCosts.razor` | `SaveRunningCostRequest` |
| `Components/Pages/Admin/Rounds/EnterResults.razor` | `List<MatchResultDto>` (no list validator exists - see Phase 2 step 2.3) |

(`ForgotPassword.razor` and `ResetPassword.razor` use `DataAnnotationsValidator` with local models and are unaffected.)

---

## Phase 1 - Revive client-side validation

Small, immediate, no behavioural risk: it restores what the January decision assumed already worked.

### Step 1.1 - Add the FluentValidation DI extensions package to Web.Client

`AddValidatorsFromAssemblyContaining` lives in `FluentValidation.DependencyInjectionExtensions`, which Web.Client does not reference (the Validators project brings in only the core `FluentValidation` 12.1.1 package; the DI extensions are referenced by Application, which Web.Client does not reference).

Edit `src/ThePredictions.Web.Client/ThePredictions.Web.Client.csproj` and add to the existing first `<ItemGroup>` of package references:

```xml
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
```

Version note: the repo rule is to pin the highest compatible version, but FluentValidation packages must stay on a single version across the dependency graph, and the solution pins 12.1.1 in both `ThePredictions.Validators.csproj` and `ThePredictions.Application.csproj`. Pin 12.1.1 here; if the solution later upgrades FluentValidation, upgrade all three references together.

### Step 1.2 - Register the validators in the client container

Edit `src/ThePredictions.Web.Client/DependencyInjection.cs`. Current start of the method:

```csharp
public static void AddClientServices(this IServiceCollection services)
{
    services.AddAuthorizationCore();
    services.AddBlazoredLocalStorage();
```

Add one line at the top of the method body, plus two usings:

```csharp
using FluentValidation;
using ThePredictions.Validators.Authentication;
```

```csharp
public static void AddClientServices(this IServiceCollection services)
{
    services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
    services.AddAuthorizationCore();
    services.AddBlazoredLocalStorage();
```

`LoginRequestValidator` is the same marker type the API uses at `src/ThePredictions.API/DependencyInjection.cs` line 213, so both containers always scan the same assembly. Default registration lifetime is Scoped (in WASM the root scope lives for the app session, which is fine - but see step 1.4 for why no rule may capture the clock at construction time).

### Step 1.3 - Harden FluentValidationValidator so a missing validator can never silently no-op again

Edit `src/ThePredictions.Web.Client/Components/Shared/FluentValidationValidator.cs`. Current code (lines 24-42):

```csharp
protected override void OnInitialized()
{
    if (EditContext is null)
        throw new InvalidOperationException(
            $"{nameof(FluentValidationValidator)} requires a cascading parameter of type {nameof(EditContext)}. " +
            $"Ensure this component is used inside an EditForm.");

    _messageStore = new ValidationMessageStore(EditContext);
    _validator = GetValidatorForModel(EditContext.Model);

    EditContext.OnValidationRequested += HandleValidationRequested;
    EditContext.OnFieldChanged += HandleFieldChanged;
}

private IValidator? GetValidatorForModel(object model)
{
    var validatorType = typeof(IValidator<>).MakeGenericType(model.GetType());
    return ServiceProvider.GetService(validatorType) as IValidator;
}
```

Change: inject a logger and log an **error** when no validator resolves. Add this injected property next to the existing `ServiceProvider` property:

```csharp
[Inject]
private ILogger<FluentValidationValidator> Logger { get; init; } = null!;
```

and insert after the `_validator = GetValidatorForModel(EditContext.Model);` line:

```csharp
if (_validator is null)
    Logger.LogError(
        "No IValidator<{ModelType}> is registered in DI; this form will submit without client-side validation",
        EditContext.Model.GetType().Name);
```

No extra `using` is needed - the Blazor WASM SDK's implicit usings already cover `Microsoft.Extensions.Logging` (`Program.cs` uses `LogLevel.Warning` without one).

**Decision: log an error, do not throw.** A `#if DEBUG` throw was considered and rejected: several forms legitimately bind models that have no FluentValidation validator (`EnterResults.razor` binds a `List<MatchResultDto>` until step 2.3 adds a list validator; `EmailSettings.razor` and `PricingSettings.razor` bind local form models; `Details.razor` binds `UserDetailsViewModel`), so throwing would break working pages in development the moment this ships. An error-level log passes the client's `SetMinimumLevel(LogLevel.Warning)` filter (`Program.cs` line 8), is loud in the browser console during development, and Phase 2 makes the server the enforcement point regardless - the client layer is UX, not security.

### Step 1.4 - Fix the construction-time clock capture in the two league validators

Now that client validation revives, this dormant bug becomes live: validator instances can be long-lived (the WASM root scope lives for the whole app session, and a Blazor form holds its validator for the life of the form), and `GreaterThan(DateTime.UtcNow)` evaluates `DateTime.UtcNow` **once, when the validator is constructed**. A user who keeps the Create League page open would be validated against a stale clock. June audit item 4.1 requires this fix too.

`src/ThePredictions.Validators/Leagues/CreateLeagueRequestValidator.cs` (lines 26-27), current:

```csharp
RuleFor(x => x.EntryDeadlineUtc)
    .GreaterThan(DateTime.UtcNow).WithMessage("Entry deadline must be in the future.");
```

Replace with:

```csharp
RuleFor(x => x.EntryDeadlineUtc)
    .Must(deadline => deadline > DateTime.UtcNow).WithMessage("Entry deadline must be in the future.");
```

`src/ThePredictions.Validators/Leagues/UpdateLeagueRequestValidator.cs` (lines 22-23): make the identical change.

Why `.Must` and not a `GreaterThan` overload: FluentValidation 12's `GreaterThan` overloads take either a constant value (evaluated at rule-build time - the bug) or a lambda selecting **another property of the same model**; neither defers an external clock. `.Must(...)` executes its predicate on every validation run, which is the supported mechanism. (`IDateTimeProvider` injection is deliberately not used here: these validators are constructed parameterlessly by assembly scanning in both containers and ship to the browser; the Validators project has no dependency on Application abstractions.)

Run `dotnet test tests/Unit/ThePredictions.Validators.Tests.Unit` afterwards and update any test that relied on the construction-time snapshot.

### Step 1.5 - Manual verification

1. `dotnet run --project src/ThePredictions.Web`
2. Browse to `/login`, submit the empty form: expect "Please enter your email address." and "Please enter your password." under the fields (from `LoginRequestValidator`), and no navigation.
3. Repeat on `/register` and the Create League form (name shorter than 3 characters must be rejected client-side).
4. Open the browser console: no "No IValidator<...> is registered" errors for these forms.

---

## Phase 2 - Enforce validation server-side at the API boundary

### Design decisions

- **Mechanism:** a global MVC action filter (`IAsyncActionFilter`). Endpoint filters are minimal-API-only; this API is controller-based. For each action argument, resolve `IValidator<runtime type>` from the request's DI scope and run `ValidateAsync`. This runs the **existing request-DTO validators** at the boundary - no duplicate command/query validators (January's "Option B" effort objection stays answered).
- **On failure: throw `FluentValidation.ValidationException`** rather than short-circuiting a 400 in the filter. `ErrorHandlingMiddleware` already maps this exception today (`src/ThePredictions.API/Middleware/ErrorHandlingMiddleware.cs` lines 41-45) to 400 with `{ errors = ex.Errors }`, so this works immediately. It also keeps a single exception-to-response mapping point: the error-contract standardisation plan (being written in parallel at `docs/todo/architecture/error-contract-standardisation/README.md`) maps `ValidationException` to a 400 `ApiErrorResponse` with an `Errors` dictionary - when that lands, only the middleware changes and this filter needs no edit. (Transitional note: until that plan ships, the response body is the middleware's current `{ "errors": [...] }` shape.)
- **Validator lifetime:** validators are registered **Scoped** at `src/ThePredictions.API/DependencyInjection.cs` line 213 (`AddValidatorsFromAssemblyContaining` default). The filter resolves via `context.HttpContext.RequestServices`, which is the per-request scope, so lifetimes are correct.
- **MediatR `ValidationBehaviour` stays as-is.** It is harmless (always a pass-through today) and becomes useful if command/query-level validators are ever added.

### Step 2.1 - Create the filter

New file `src/ThePredictions.API/Filters/FluentValidationActionFilter.cs` (the `Filters` folder already exists and holds `ApiKeyAuthoriseAttribute.cs`):

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ThePredictions.API.Filters;

/// <summary>
/// Runs the FluentValidation validator (if one is registered) for every bound action argument,
/// so the server enforces the same rules as the browser. Throws
/// <see cref="ValidationException"/> on failure; <c>ErrorHandlingMiddleware</c> maps it to a 400.
/// </summary>
public class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
                throw new ValidationException(result.Errors);
        }

        await next();
    }
}
```

Notes:

- `argument.GetType()` (the runtime type) is used so bound bodies resolve their concrete validator; `ValidationContext<object>` wrapping the concrete instance is the same pattern the client's `FluentValidationValidator` already uses and is supported by `AbstractValidator<T>`.
- Null arguments are skipped deliberately: `AuthenticationController.RefreshTokenAsync` allows an empty body (`EmptyBodyBehavior.Allow`) and reads the token from the cookie.
- Route/query primitives (`int leagueId` etc.) resolve no validator and pass through; that is by design (see coverage table).

### Step 2.2 - Register the filter globally

Edit `src/ThePredictions.API/DependencyInjection.cs`. Current (line 24):

```csharp
services.AddControllers();
```

Replace with:

```csharp
services.AddControllers(options => options.Filters.Add<FluentValidationActionFilter>());
```

and add `using ThePredictions.API.Filters;` to the usings. The filter has no constructor dependencies, so `Filters.Add<T>()` type activation is fine.

### Step 2.3 - Close the validator coverage gaps

Cross-reference of every request type bound from a request body against `src/ThePredictions.Validators` (verified July 2026):

| Request type | Validator | Action |
|---|---|---|
| `LoginRequest`, `RegisterRequest`, `RefreshTokenRequest`, `RequestPasswordResetRequest`, `ResetPasswordRequest` | Yes | None |
| `UpdateUserDetailsRequest`, `SetPayoutDetailsRequest` | Yes | None |
| `CreateLeagueRequest`, `UpdateLeagueRequest`, `JoinLeagueRequest`, `DefinePrizeStructureRequest`, `PrizeSchemeRequest` | Yes | None |
| `SubmitPredictionsRequest`, `ApplyBoostRequest`, `SetLeagueBoostRulesRequest` | Yes | None |
| `Create/UpdateCompetitionRequest`, `Create/UpdateMatchRequest`, `Create/UpdateRoundRequest`, `Create/UpdateSeasonRequest`, `Create/UpdateTeamRequest` | Yes | None |
| `UpdatePricingSettingsRequest`, `UpdateServiceFeeRequest`, `SaveRunningCostRequest`, `DeleteUserRequest`, `UpdateUserRoleRequest` | Yes | None |
| `MatchResultDto` | Yes (element) | **Add list validator** - `RoundsController.UpdateMatchResults` binds `List<MatchResultDto>` (line 130); `IValidator<List<MatchResultDto>>` does not resolve |
| `AcquireSeasonPassRequest` | **No** | **Add validator** (below) |
| `ConfirmEmailRequest` | **No** | **Add validator** (below) |
| `EvaluateSchemeRequest` | **No** | **Add validator** (below) |
| `SendTestEmailRequest` | **No** | **Add validator** (below) |
| `UpdateEmailSettingsRequest` | **No** | **None needed, recorded:** single `bool EmailsEnabled`; every representable value is valid |
| `PrizeSchemeCategoryRequest` | Via parent | **None needed, recorded:** validated by `PrizeSchemeRequestValidator`'s `RuleForEach(...).ChildRules(...)`; never bound as a top-level body |
| `LeagueRequestDto` (Dashboard) | No | **None needed, recorded:** a response DTO (a pending join request) despite the name; never bound from a request body |
| `BaseCompetition/Match/Season/TeamRequest` | Via subclass | **None needed, recorded:** abstract bases, validated through the Create/Update validators |
| `[FromBody] string theme` (`AccountController.UpdateThemePreferenceAsync`, line 60) | No | **None needed, recorded:** `UpdateThemePreferenceCommandHandler` normalises any input with `request.Theme is "dark" ? "dark" : "light"` - the whole input domain is safe |
| `[FromBody] bool isActive` (`Admin/SeasonsController.UpdateStatusAsync`, line 260) | No | **None needed, recorded:** every `bool` is valid |
| `[FromBody] LeagueMemberStatus newStatus` (`LeaguesController.UpdateLeagueMemberStatusAsync`, line 544) | No | **None needed, recorded:** invalid transitions are rejected by the domain and mapped to 400 by the middleware. (JSON can bind an out-of-range enum integer; the domain guard is the backstop) |

**Raw-primitive bodies - stated approach:** keep them; do not wrap in DTOs. Both current primitive bodies are either total over their value domain (`bool`) or normalised in the handler (`theme`). Going forward, new endpoints must bind a request DTO with a validator; a bare primitive body is only acceptable when literally every representable value is valid, and the justification must be recorded in this table.

New validators (all picked up automatically by the assembly scans in both the API and, after Phase 1, the client):

`src/ThePredictions.Validators/SeasonPasses/AcquireSeasonPassRequestValidator.cs` - the handler guards `SeasonId` with Ardalis (`ArgumentException`, mapped to 400 with a raw message); the validator produces the standard errors payload instead:

```csharp
using FluentValidation;
using ThePredictions.Contracts.SeasonPasses;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Validators.SeasonPasses;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class AcquireSeasonPassRequestValidator : AbstractValidator<AcquireSeasonPassRequest>
{
    public AcquireSeasonPassRequestValidator()
    {
        RuleFor(x => x.SeasonId)
            .GreaterThan(0).WithMessage("You must select a valid season.");
    }
}
```

`src/ThePredictions.Validators/Authentication/ConfirmEmailRequestValidator.cs` - the handler looks the token up verbatim; reject the obviously malformed before the database round-trip:

```csharp
using FluentValidation;
using ThePredictions.Contracts.Authentication;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Validators.Authentication;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("The confirmation token is required.")
            .MaximumLength(512).WithMessage("The confirmation token is not valid.");
    }
}
```

`src/ThePredictions.Validators/Prizes/EvaluateSchemeRequestValidator.cs` - the handler truncates `Price`/`PrizeFundOverride` to `int` pounds and feeds `EntrantCount` straight into the evaluator, so negative or absurd inputs corrupt the preview maths; rules mirror `CreateLeagueRequestValidator` so the editor preview and the create form agree:

```csharp
using FluentValidation;
using ThePredictions.Contracts.Prizes;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Validators.Prizes;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class EvaluateSchemeRequestValidator : AbstractValidator<EvaluateSchemeRequest>
{
    public EvaluateSchemeRequestValidator()
    {
        RuleFor(x => x.SeasonId)
            .GreaterThan(0).WithMessage("You must select a valid season.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be 0 or greater.")
            .LessThanOrEqualTo(10000).WithMessage("Price must not exceed 10,000.");

        RuleFor(x => x.PrizeFundOverride)
            .GreaterThanOrEqualTo(0).WithMessage("The prize fund top-up must be 0 or greater.")
            .When(x => x.PrizeFundOverride.HasValue);

        RuleFor(x => x.EntrantCount)
            .InclusiveBetween(0, 10000).WithMessage("Entrant count must be between 0 and 10,000.");

        RuleFor(x => x.Scheme)
            .SetValidator(new PrizeSchemeRequestValidator());
    }
}
```

`src/ThePredictions.Validators/Admin/EmailTests/SendTestEmailRequestValidator.cs` - the handler forwards `TemplateId` and `Parameters` straight to Brevo; bound the payload:

```csharp
using FluentValidation;
using ThePredictions.Contracts.Admin.EmailTests;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Validators.Admin.EmailTests;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class SendTestEmailRequestValidator : AbstractValidator<SendTestEmailRequest>
{
    public SendTestEmailRequestValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("You must select a valid template.");

        RuleFor(x => x.Parameters)
            .NotNull().WithMessage("Parameters are required (an empty set is allowed).")
            .Must(parameters => parameters.Count <= 50).WithMessage("A test email cannot have more than 50 parameters.")
            .When(x => x.Parameters is not null, ApplyConditionTo.CurrentValidator);

        RuleForEach(x => x.Parameters)
            .Must(parameter => !string.IsNullOrWhiteSpace(parameter.Key)).WithMessage("Parameter names cannot be empty.")
            .Must(parameter => parameter.Value is null || parameter.Value.Length <= 1000).WithMessage("Parameter values cannot exceed 1,000 characters.")
            .When(x => x.Parameters is not null);
    }
}
```

`src/ThePredictions.Validators/Admin/Matches/MatchResultListValidator.cs` - covers the `List<MatchResultDto>` body on `RoundsController.UpdateMatchResults` and, as a bonus, revives client-side validation on `EnterResults.razor` (which binds the same list type):

```csharp
using FluentValidation;
using ThePredictions.Contracts.Admin.Matches;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Validators.Admin.Matches;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class MatchResultListValidator : AbstractValidator<List<MatchResultDto>>
{
    public MatchResultListValidator()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("At least one match result is required.");

        RuleForEach(x => x)
            .SetValidator(new MatchResultDtoValidator());
    }
}
```

Add unit tests for each new validator in `tests/Unit/ThePredictions.Validators.Tests.Unit`, following the existing `Validate_ShouldFail_WhenX` naming.

### Step 2.4 - Regression tests proving server-side rejection (June 1.1 requirement)

No API integration test project exists yet (`ThePredictions.API.Tests.Integration` is planned as Phase 6 of `docs/todo/architecture/test-suite/README.md`). **Decision:** unit-test the filter now in `tests/Unit/ThePredictions.Composition.Tests.Unit` - it is the only existing test project that references `ThePredictions.API` - and add full-pipeline `WebApplicationFactory` tests when the Phase 6 project is created.

New file `tests/Unit/ThePredictions.Composition.Tests.Unit/Filters/FluentValidationActionFilterTests.cs` (no mocking framework needed; the project deliberately has none):

```csharp
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ThePredictions.API.Filters;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Validators.Authentication;
using Xunit;

namespace ThePredictions.Composition.Tests.Unit.Filters;

public class FluentValidationActionFilterTests
{
    private static ActionExecutingContext BuildContext(object? argument)
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        var arguments = new Dictionary<string, object?> { ["request"] = argument };

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            arguments,
            controller: new object());
    }

    private static ActionExecutionDelegate Next(ActionExecutingContext context, Action onInvoked) =>
        () =>
        {
            onInvoked();
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), controller: new object()));
        };

    [Fact]
    public async Task OnActionExecutionAsync_ShouldThrowValidationException_WhenLeagueNameIsOverlongHtml()
    {
        var request = new CreateLeagueRequest
        {
            SeasonId = 1,
            Name = new string('a', 190) + "<script>x</script>",
            Price = 10,
            EntryDeadlineUtc = DateTime.UtcNow.AddDays(7),
            PointsForExactScore = 5,
            PointsForCorrectResult = 2
        };
        var context = BuildContext(request);
        var filter = new FluentValidationActionFilter();

        var act = () => filter.OnActionExecutionAsync(context, Next(context, () => { }));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldInvokeAction_WhenRequestIsValid()
    {
        var request = new CreateLeagueRequest
        {
            SeasonId = 1,
            Name = "A Perfectly Fine League",
            Price = 10,
            EntryDeadlineUtc = DateTime.UtcNow.AddDays(7),
            PointsForExactScore = 5,
            PointsForCorrectResult = 2
        };
        var context = BuildContext(request);
        var filter = new FluentValidationActionFilter();
        var actionInvoked = false;

        await filter.OnActionExecutionAsync(context, Next(context, () => actionInvoked = true));

        actionInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldInvokeAction_WhenArgumentHasNoValidator()
    {
        var context = BuildContext(argument: 42);
        var filter = new FluentValidationActionFilter();
        var actionInvoked = false;

        await filter.OnActionExecutionAsync(context, Next(context, () => actionInvoked = true));

        actionInvoked.Should().BeTrue();
    }
}
```

The first test is the June 1.1 regression proof: a 200-character league name containing HTML is rejected server-side with no client involved.

When the `ThePredictions.API.Tests.Integration` project is created (test-suite plan Phase 6), add an end-to-end test that POSTs the same invalid body to `/api/leagues` through `WebApplicationFactory` and asserts a 400 with an `errors` payload.

### Step 2.5 - Documentation updates

**(a) `src/ThePredictions.API/CLAUDE.md`** - the Validation section currently claims validation happens, which has been false. Current text (the whole `## Validation` section at the end of the file):

```markdown
## Validation

Validation happens automatically via the `ValidationBehaviour` pipeline:

1. Request comes in
2. FluentValidation validator runs (if exists)
3. If invalid, throws `ValidationException` → 400 response
4. If valid, handler executes

You don't need to manually validate in controllers or handlers.
```

Replace with:

```markdown
## Validation

Request validation runs at the API boundary via the global `FluentValidationActionFilter` (`Filters/FluentValidationActionFilter.cs`), registered in `DependencyInjection.cs`:

1. Request comes in and model binding populates the action arguments
2. For each argument, the filter resolves `IValidator<T>` from the request scope (validators live in `ThePredictions.Validators` and target Contracts request DTOs)
3. If invalid, the filter throws `FluentValidation.ValidationException`, which `ErrorHandlingMiddleware` maps to a 400 response
4. If valid (or no validator is registered for the argument type), the action executes

You don't need to manually validate in controllers or handlers. The MediatR `ValidationBehaviour` remains in the pipeline as a second layer, but it only fires for validators targeting command/query types (none exist today).

When adding a new request DTO, add a matching validator in `ThePredictions.Validators` - arguments without a validator pass through unvalidated. Bare primitive request bodies are only acceptable when every representable value is valid.
```

**(b) `docs/guides/cqrs-patterns.md`** (June 1.1 checklist item: document where validation runs). Current text:

```markdown
## MediatR Pipeline Behaviours

Requests flow through these behaviours in order:

1. **ValidationBehaviour** - Runs FluentValidation validators, throws if invalid
2. **TransactionBehaviour** - Wraps `ITransactionalRequest` in TransactionScope
```

Replace with:

```markdown
## Where Validation Runs

FluentValidation validators live in `ThePredictions.Validators` and target **Contracts request DTOs**, not commands or queries. They execute in two places:

1. **API boundary (enforcement):** the global `FluentValidationActionFilter` in `ThePredictions.API` resolves `IValidator<T>` for every bound action argument and throws `ValidationException` (mapped to 400 by `ErrorHandlingMiddleware`) on failure. This is the security boundary - a direct API call cannot bypass it.
2. **Blazor client (UX):** the `FluentValidationValidator` component resolves the same validators from the client container for inline form messages. This layer is convenience only and must never be the sole check.

## MediatR Pipeline Behaviours

Requests flow through these behaviours in order:

1. **ValidationBehaviour** - Runs FluentValidation validators registered for the command/query type, throws if invalid. Note: all current validators target request DTOs (validated at the API boundary, above), so this behaviour is a pass-through unless a command/query validator is added
2. **TransactionBehaviour** - Wraps `ITransactionalRequest` in TransactionScope
```

### Step 2.6 - Build and full verification

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests/Unit/ThePredictions.Validators.Tests.Unit
dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit
tools\Test Coverage\coverage-unit.bat
```

Manual server-side proof without the client: run `dotnet run --project src/ThePredictions.API`, obtain a JWT via `/api/authentication/login` in Swagger, then POST to `/api/leagues` a body with a 200-character `name` containing `<script>` - expect HTTP 400 with an `errors` payload, and no league created.

---

## Out of scope

- The error contract response shape (`ApiErrorResponse` with an `Errors` dictionary) - owned by the parallel error-contract-standardisation plan; this plan deliberately throws `ValidationException` so only the middleware changes when that lands.
- Creating the `ThePredictions.API.Tests.Integration` project - test-suite plan Phase 6 (`docs/todo/architecture/test-suite/README.md`); this plan notes the end-to-end test to add there.
- Command/query-level validators (January's "Option B") - not needed once the boundary filter runs the request-DTO validators.
- The remaining June audit item 4.1 work (injecting `IDateTimeProvider` into the seven backend files, `UtcInputDate.razor`) - only the two validator deadline rules are fixed here.
- Migrating `ForgotPassword.razor` / `ResetPassword.razor` from DataAnnotations to FluentValidation - they validate correctly today.
- Domain guard gaps that overlap validator rules (e.g. `League.Create` only guarding empty names) - defence in depth improvements tracked by the June audit, not this plan.

## Verification checklist

- [ ] `FluentValidation.DependencyInjectionExtensions` 12.1.1 added to `ThePredictions.Web.Client.csproj`
- [ ] `AddValidatorsFromAssemblyContaining<LoginRequestValidator>()` called in `Web.Client/DependencyInjection.cs`
- [ ] `FluentValidationValidator` logs an error when no validator resolves (no more silent no-op)
- [ ] `CreateLeagueRequestValidator` and `UpdateLeagueRequestValidator` deadline rules use `.Must(...)` evaluated at validation time
- [ ] Empty login form submission shows field messages in the browser (manual)
- [ ] `FluentValidationActionFilter` created in `src/ThePredictions.API/Filters/` and registered globally in `AddControllers`
- [ ] New validators added: `AcquireSeasonPassRequestValidator`, `ConfirmEmailRequestValidator`, `EvaluateSchemeRequestValidator`, `SendTestEmailRequestValidator`, `MatchResultListValidator`, each with unit tests
- [ ] Coverage table above matches reality (every body-bound type has a validator or a recorded justification)
- [ ] Filter unit tests pass in `ThePredictions.Composition.Tests.Unit`, including the 200-character-HTML league name rejection (June 1.1 regression proof)
- [ ] Manual direct-API test: invalid POST to `/api/leagues` returns 400 without the client
- [ ] `src/ThePredictions.API/CLAUDE.md` Validation section replaced (exact text in step 2.5a)
- [ ] `docs/guides/cqrs-patterns.md` documents where validation runs (exact text in step 2.5b)
- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` is clean
- [ ] Full-pipeline `WebApplicationFactory` test noted for test-suite Phase 6
