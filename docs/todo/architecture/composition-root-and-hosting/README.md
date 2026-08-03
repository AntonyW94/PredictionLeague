# Composition Root And Hosting Consolidation

## Status

**Not Started** | In Progress | Complete

## Priority

**High.** The production host (`ThePredictions.Web`) serves every response **without any security headers** (no CSP, no X-Content-Type-Options, no X-Frame-Options, no Referrer-Policy, no Permissions-Policy) because `UseSecurityHeaders()` is only wired into the standalone `ThePredictions.API` host, which is not what gets deployed. Fixing that is Phase 1 and is deliberately small so it can ship on its own. The remaining phases are Medium priority structural work: application-layer registrations are smeared across the Infrastructure and API `DependencyInjection` classes, the two host `Program.cs` files have drifted (the standalone API host runs with empty `IOptions` because it never binds the options sections the Web host binds), and several package/project references sit in the wrong project.

> **Amendment 2026-08-03 (EctManager comparison review).** When the options bindings move into
> the owning layers, bind them with `AddOptions<T>().Bind(...).Validate(...).ValidateOnStart()`
> rather than plain `services.Configure<T>(...)`. Nothing currently validates any settings class,
> so a missing Stripe key, Brevo template ID or connection string surfaces as a runtime 500 on the
> first request that needs it - which is exactly the misfiling ADR-0016 was written about.
> `ValidateOnStart()` turns it into a boot failure naming the missing key. EctManager does this for
> every settings class in its worker services.

All findings below were verified against the code on 2026-07-06. Line numbers refer to the files as they stand on `master` at that date; re-check them before editing because nearby commits may shift them slightly. **Where the code differs from this document, follow the code.**

## Relationship to the 2026-06 code review audit

See [`../code-consistency-audit/2026-06-code-review-findings.md`](../code-consistency-audit/2026-06-code-review-findings.md):

- Item **4.9** contains the bullet "Remove the accidental `Microsoft.AspNetCore.Components.WebAssembly.DevServer` package references from the API and Infrastructure projects". **Phase 4 of this plan subsumes that bullet with fuller detail** (it also moves `Microsoft.AspNetCore.Components.WebAssembly.Server` to the project that actually needs it). Tick that audit checkbox when Phase 4 lands.
- Item **4.8** notes `AuthControllerBase` parses `RefreshTokenExpiryDays` with culture-sensitive `double.Parse`. The `JwtSettings` options migration in Phase 2 removes that `double.Parse` entirely, which resolves the second bullet of 4.8 (the first bullet, about month names, is untouched).
- The bullet at line 175 ("Move `CleanupResult` and `SecurityHeadersMiddlewareExtensions` into their own files") is half-resolved by Phase 1, which splits `SecurityHeadersMiddlewareExtensions` into its own file. `CleanupResult` is out of scope here.

## The guard rail: ContainerValidationTests

`tests\Unit\ThePredictions.Composition.Tests.Unit\ContainerValidationTests.cs` builds the **real** container by calling `AddInfrastructureServices` + `AddApiServices` against an in-memory configuration, then resolves every MediatR handler. It exists precisely to catch the kind of registration moves this plan makes. It must pass after **every** phase:

```
dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit/ThePredictions.Composition.Tests.Unit.csproj
```

How each phase affects it:

- **Phase 1** (middleware only): no container changes; the test is unaffected but run it anyway.
- **Phase 2**: the test currently replicates the Web host's out-of-band options binding at lines 66-72 (`services.Configure<BrevoSettings>(...)` etc. under the comment "Options the host binds in Program.cs (outside the two Add* methods)"). Phase 2 moves those bindings **inside** `AddApiServices`, so Phase 2 includes deleting those lines from the test (step 2.7). The in-memory configuration dictionary (lines 30-48) already contains every key the moved bindings read, including the full `JwtSettings` section, so no dictionary changes are needed.
- **Phase 3** (pipeline extraction): no `IServiceCollection` changes; unaffected but run it.
- **Phase 4** (references): the test project references the API and Infrastructure projects directly and uses no WebAssembly types; unaffected but run it.

Standard verification after each phase (run all three):

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit/ThePredictions.Composition.Tests.Unit.csproj
dotnet test ThePredictions.sln
```

## Verified current state

**Two host pipelines exist and have drifted:**

- `src\ThePredictions.Web\Program.cs` is the deployed host. It hosts the API in-process: `builder.Services.AddApiServices(builder.Configuration)` (line 62), `app.UseMiddleware<CorrelationIdMiddleware>()` / `app.UseMiddleware<ErrorHandlingMiddleware>()` (lines 129-130), `app.MapControllers()` (line 165). It has Serilog, CORS, forwarded headers, apex-to-www redirect, DataProtection, `DatabaseInitialiser`, Key Vault config, `EnableSubstitutions()`, Blazor hosting and static files. It does **not** call `UseSecurityHeaders()`.
- `src\ThePredictions.API\Program.cs` is a second, standalone pipeline. It **does** call `app.UseSecurityHeaders()` (line 20) but lacks everything host-grade: no Serilog, no CORS, no forwarded headers, no DataProtection, no `DatabaseInitialiser`, no Key Vault, no `EnableSubstitutions()`, and **no options binding at all**.

**Options binding is host-local:** `src\ThePredictions.Web\Program.cs` lines 65-70 bind `BrevoSettings`, `EmailDeliverySettings`, `FootballApiSettings`, `FootballApiResilienceSettings`, `TimeoutSettings` and `SiteSettings`. The API host never binds any of them, so when run standalone every `IOptions<T>` consumer (for example `BrevoEmailService`, `FootballDataService`, `FootballApiHealthCheck`) resolves with default/empty values.

**Application-layer registrations are smeared:**

- There is **no** `DependencyInjection` class in `src\ThePredictions.Application`.
- `src\ThePredictions.Infrastructure\DependencyInjection.cs` registers Application classes: `FieldEncryptionService` (line 38, class lives in `ThePredictions.Application.Services`), the five `IPrizeStrategy` implementations from `ThePredictions.Application.Features.Admin.Rounds.Strategies` (lines 118-122), `PredictionDomainService` (line 125, a **Domain** service from `ThePredictions.Domain.Services`), `EmailTestDefaultsResolver` (line 134), `SeasonAccessService` / `SeasonPriceRecommendationService` / `EmailConfirmationSender` (lines 147-149) - all Application classes. It also binds the Application-owned `FieldEncryptionSettings` (lines 36-37).
- `src\ThePredictions.API\DependencyInjection.cs` has a private `AddApplicationServices` (lines 211-230) registering MediatR (with `ValidationBehaviour` / `TransactionBehaviour` open behaviours and the `MediatR:LicenceKey` licence key), `PrizeEvaluator`, `PrizeEvaluationInputsReader`, `PrizeSchemeFreezeService` (all Application classes) and the FluentValidation validators, alongside genuinely API-owned items (`AddHttpContextAccessor`, `CurrentUserService`).

**JwtSettings bypasses the options pattern - and the registered singleton is dead:** `src\ThePredictions.API\DependencyInjection.cs` lines 165-167:

```csharp
var jwtSettings = new JwtSettings();
configuration.Bind(JwtSettings.SectionName, jwtSettings);
services.AddSingleton(jwtSettings);
```

A solution-wide grep for `JwtSettings` shows **nothing injects that singleton**. The actual consumers read raw `IConfiguration`: `AuthenticationTokenService` (`src\ThePredictions.Infrastructure\Services\AuthenticationTokenService.cs` line 30, `configuration.GetSection("JwtSettings")` plus four `double.Parse` / indexer reads) and `AuthControllerBase` (`src\ThePredictions.API\Controllers\AuthControllerBase.cs` line 18, `double.Parse(configuration["JwtSettings:RefreshTokenExpiryDays"]!)`). `AddAppAuthentication` (API DI line 178) also reads the section at registration time via `Get<JwtSettings>()`, which is legitimate (JwtBearer must be configured before the container is built) and stays.

**Package/reference misplacement:**

- `src\ThePredictions.Infrastructure\ThePredictions.Infrastructure.csproj` lines 17-20 reference `Microsoft.AspNetCore.Components.WebAssembly.DevServer` 8.0.14 and `Microsoft.AspNetCore.Components.WebAssembly.Server` 8.0.14 (`TreatAsUsed`). Infrastructure code uses neither; they exist purely so `Web\Program.cs` gets `UseWebAssemblyDebugging()` (line 109) and `UseBlazorFrameworkFiles()` (line 133) transitively (Web -> API -> Infrastructure). `Web.csproj` declares neither.
- `src\ThePredictions.API\ThePredictions.API.csproj` line 15 references `Microsoft.AspNetCore.Components.WebAssembly.DevServer` for no reason at all.
- `src\ThePredictions.Application\ThePredictions.Application.csproj` line 28 references `ThePredictions.Validators`, but a grep for `ThePredictions.Validators` in Application source finds **zero** usages (only the csproj line itself). The API's `AddValidatorsFromAssemblyContaining<LoginRequestValidator>()` (API DI line 213, `using ThePredictions.Validators.Authentication` at line 14) compiles against the API's **own direct** reference (`API.csproj` line 30), and Web (line 15) and Web.Client (line 24) also reference Validators directly, so removing the Application reference is safe.
- `Web\Program.cs` uses `EnableSubstitutions()` (lines 7 and 43) from `ThePredictions.Hosting.Shared`, but `Web.csproj` has no direct reference to it (transitive via API).
- `Web\Program.cs` line 60 calls `builder.Services.AddControllers()` even though `AddApiServices` also calls it (API DI line 24) - a harmless duplicate, removed in Phase 2.

**Settings classes are split across two homes:** `src\ThePredictions.Application\Configuration\` holds `BrevoSettings`, `EmailDeliverySettings`, `FieldEncryptionSettings`, `FootballApiResilienceSettings`, `FootballApiSettings`, `SiteSettings`, `TemplateSettings`, `TimeoutSettings`; `src\ThePredictions.Infrastructure\Authentication\Settings\` holds `JwtSettings` and `GoogleAuthSettings`. See "Follow-ups" for the target convention.

---

## Phase 1: Security headers on the Web host (small, urgent, ship alone)

Goal: production responses carry the same security headers the standalone API host already emits, with a CSP adjusted for the Blazor WASM client's real external dependencies. Applying the API's CSP verbatim would **break the site**: `src\ThePredictions.Web.Client\wwwroot\index.html` loads Bootstrap, Bootstrap Icons and SweetAlert2 CSS/JS from `https://cdn.jsdelivr.net` (lines 27-29 and 101-102) and contains two inline `<script>` blocks (lines 46-58 and 66-96), and the production CSS bundle prepends `@import url('https://fonts.googleapis.com/...')` (`Web.csproj` line 166, fonts served from `https://fonts.gstatic.com`). League logos are arbitrary user-supplied https URLs, so `img-src` needs `https:`.

### Step 1.1: Make the CSP configurable

Create `src\ThePredictions.API\Middleware\SecurityHeadersOptions.cs`:

```csharp
namespace ThePredictions.API.Middleware;

public class SecurityHeadersOptions
{
    /// <summary>
    /// Emitted as the Content-Security-Policy header. Defaults to the strict
    /// API-only policy (no external origins, no inline scripts).
    /// </summary>
    public string ContentSecurityPolicy { get; init; } =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' https://accounts.google.com; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self'; " +
        "upgrade-insecure-requests;";
}
```

(The default string is character-for-character the CSP currently hardcoded in `SecurityHeadersMiddleware.cs` lines 44-54, so the API host's behaviour does not change.)

### Step 1.2: Parameterise the middleware

In `src\ThePredictions.API\Middleware\SecurityHeadersMiddleware.cs`, change the class signature from:

```csharp
public class SecurityHeadersMiddleware(RequestDelegate next)
```

to:

```csharp
public class SecurityHeadersMiddleware(RequestDelegate next, SecurityHeadersOptions options)
```

and replace the hardcoded CSP block (lines 39-54, the comment plus the `context.Response.Headers.Append("Content-Security-Policy", ...)` call with its ten concatenated strings) with:

```csharp
        // Content Security Policy - the exact policy is host-specific, see SecurityHeadersOptions.
        context.Response.Headers.Append("Content-Security-Policy", options.ContentSecurityPolicy);
```

All the other headers (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, HSTS-when-HTTPS, Permissions-Policy) stay exactly as they are.

### Step 1.3: Split the extensions class into its own file

Delete `SecurityHeadersMiddlewareExtensions` from the bottom of `SecurityHeadersMiddleware.cs` (currently lines 60-66) and create `src\ThePredictions.API\Middleware\SecurityHeadersMiddlewareExtensions.cs`:

```csharp
namespace ThePredictions.API.Middleware;

public static class SecurityHeadersMiddlewareExtensions
{
    public static void UseSecurityHeaders(this IApplicationBuilder builder) =>
        builder.UseSecurityHeaders(new SecurityHeadersOptions());

    public static void UseSecurityHeaders(this IApplicationBuilder builder, SecurityHeadersOptions options)
    {
        builder.UseMiddleware<SecurityHeadersMiddleware>(options);
    }
}
```

`ThePredictions.API\Program.cs` line 20 (`app.UseSecurityHeaders();`) keeps compiling unchanged via the parameterless overload.

### Step 1.4: Wire the middleware into the Web host

In `src\ThePredictions.Web\Program.cs`, immediately after line 130 (`app.UseMiddleware<ErrorHandlingMiddleware>();`) and before `app.UseHttpsRedirection();` - the same relative position the API host uses - insert:

```csharp
app.UseSecurityHeaders(new SecurityHeadersOptions
{
    // Differences from the API default, all forced by the WASM client:
    // - script-src: cdn.jsdelivr.net (Bootstrap bundle, SweetAlert2) and 'unsafe-inline'
    //   (two inline scripts in index.html - see follow-up to hash or externalise them)
    // - style-src: cdn.jsdelivr.net (Bootstrap, icons, SweetAlert2 CSS) and
    //   fonts.googleapis.com (the published app.css starts with a Google Fonts @import)
    // - font-src: fonts.gstatic.com (Google Fonts files) and cdn.jsdelivr.net (icon font)
    ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data: https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
        "connect-src 'self' https://accounts.google.com; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self'; " +
        "upgrade-insecure-requests;"
});
```

`using ThePredictions.API.Middleware;` is already present (line 5). The middleware sits before `UseBlazorFrameworkFiles` / `UseStaticFiles`, so static assets get the headers too.

### Step 1.5: Resolve the HSTS duplication

`Web\Program.cs` line 114 calls `app.UseHsts()` in the non-local branch. `SecurityHeadersMiddleware` also appends `Strict-Transport-Security` for HTTPS requests, so keeping both would emit the header twice. Delete the `app.UseHsts();` line (keep `app.UseExceptionHandler("/Error");`):

Current (lines 111-115):

```csharp
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
```

Target:

```csharp
else
{
    app.UseExceptionHandler("/Error");
}
```

Behaviour note: the middleware's HSTS value (`max-age=31536000; includeSubDomains`) is stronger than `UseHsts()`'s default (30 days, no subdomains) and is emitted in every environment when the request is HTTPS, which matches what the API host already does.

### Step 1.6: Verify

1. `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true`
2. `dotnet test ThePredictions.sln`
3. Run the site locally: `dotnet run --project src/ThePredictions.Web` and confirm with `curl -kI https://localhost:<port>/` that the response carries `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` and `Permissions-Policy`.
4. Browse the app with browser devtools open and check the console for CSP violation reports on: the home page (Google Fonts, Bootstrap, icons), login including "Sign in with Google", the dashboard (league logos - external https images), any page that fires a SweetAlert2 modal, and an admin page. Fix any missed origin by extending the relevant directive, never by deleting the header.

---

## Phase 2: An Application composition root, moved registrations, and options binding in the owning layers

Goal: each layer registers its own services and binds its own options, `AddInfrastructureServices` + `AddApiServices` remains the complete composition root for **both** hosts, and the standalone API host stops running on empty options.

### Step 2.1: Create `src\ThePredictions.Application\DependencyInjection.cs`

`Application.csproj` already has `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (lines 9-11) plus direct `MediatR` and `FluentValidation.DependencyInjectionExtensions` package references, so no csproj changes are needed for this file to compile.

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePredictions.Application.Common.Behaviours;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Options classes owned by the Application layer. Binding them here means every host
        // that composes the application gets them - previously only the Web host bound these.
        services.Configure<BrevoSettings>(configuration.GetSection("Brevo"));
        services.Configure<EmailDeliverySettings>(configuration.GetSection("EmailDelivery"));
        services.Configure<FieldEncryptionSettings>(configuration.GetSection(FieldEncryptionSettings.SectionName));
        services.Configure<FootballApiSettings>(configuration.GetSection("FootballApi"));
        services.Configure<FootballApiResilienceSettings>(configuration.GetSection("FootballApi:Resilience"));
        services.Configure<TimeoutSettings>(configuration.GetSection("Timeouts"));
        services.Configure<SiteSettings>(options => options.BaseUrl = configuration["ApiBaseUrl"]);

        services.AddSingleton<IFieldEncryptionService, FieldEncryptionService>();

        services.AddScoped<IPrizeStrategy, RoundPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, MonthlyPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, OverallPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, MostExactScoresPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, SectionPrizeStrategy>();

        services.AddSingleton<IPrizeEvaluator, PrizeEvaluator>();
        services.AddScoped<IPrizeEvaluationInputsReader, PrizeEvaluationInputsReader>();
        services.AddScoped<IPrizeSchemeFreezeService, PrizeSchemeFreezeService>();

        // Domain service. The Domain project deliberately has no DI package reference,
        // so the Application layer (its orchestrator) owns the registration.
        services.AddScoped<PredictionDomainService>();

        services.AddScoped<ISeasonAccessService, SeasonAccessService>();
        services.AddScoped<ISeasonPriceRecommendationService, SeasonPriceRecommendationService>();
        services.AddScoped<IEmailConfirmationSender, EmailConfirmationSender>();
        services.AddSingleton<IEmailTestDefaultsResolver, EmailTestDefaultsResolver>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(IAssemblyMarker).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehaviour<,>));

            var mediatRKey = configuration["MediatR:LicenceKey"];
            if (!string.IsNullOrEmpty(mediatRKey))
                cfg.LicenseKey = mediatRKey;
        });

        return services;
    }
}
```

Preserve every lifetime exactly as listed (note the singletons: `IFieldEncryptionService`, `IPrizeEvaluator`, `IEmailTestDefaultsResolver`). If the compiler reports an interface in a different namespace than the usings above suggest (for example `IPrizeEvaluator` may live in `Common.Interfaces` rather than `Common.Prizes`), trust the compiler and adjust the usings; do not move the interfaces.

### Step 2.2: Strip the moved registrations from Infrastructure

In `src\ThePredictions.Infrastructure\DependencyInjection.cs` delete:

- Lines 36-38: the `FieldEncryptionSettings` `Configure` call and `services.AddSingleton<IFieldEncryptionService, FieldEncryptionService>();`
- Lines 118-122: the five `IPrizeStrategy` registrations
- Line 125: `services.AddScoped<PredictionDomainService>();`
- Line 134: `services.AddSingleton<IEmailTestDefaultsResolver, EmailTestDefaultsResolver>();`
- Lines 147-149: `ISeasonAccessService`, `ISeasonPriceRecommendationService`, `IEmailConfirmationSender`

Everything else stays (repositories, Identity, health checks, `SqlRetryPolicyOptions`, `IDateTimeProvider`, `IEmailDateFormatter`, the email/football services, `ILeagueStatsService`, `ILeagueMembershipService`). Remove any using directives the compiler now flags as unused (likely `ThePredictions.Application.Features.Admin.Rounds.Strategies` and `ThePredictions.Domain.Services`).

### Step 2.3: Rework the API DependencyInjection

In `src\ThePredictions.API\DependencyInjection.cs`:

1. Add `using ThePredictions.Application;`.
2. Replace the whole private `AddApplicationServices` method (lines 211-230) with a private method containing only the genuinely API-owned registrations:

```csharp
        private static void AddApiHostServices(this IServiceCollection services)
        {
            services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
        }
```

3. In `AddApiServices`, replace the call `services.AddApplicationServices(configuration);` (line 171) with:

```csharp
            services.AddApplicationServices(configuration);
            services.AddApiHostServices();
```

(`AddApplicationServices` now resolves to the new public extension in `ThePredictions.Application`.) The FluentValidation registration stays in the API layer on purpose: the validators validate API request DTOs, the API project holds the direct `ThePredictions.Validators` reference, and keeping it here is what makes the Phase 4 removal of Application's dead Validators reference safe.

### Step 2.4: Migrate JwtSettings to the options pattern

In `src\ThePredictions.API\DependencyInjection.cs`, replace lines 165-167:

```csharp
            var jwtSettings = new JwtSettings();
            configuration.Bind(JwtSettings.SectionName, jwtSettings);
            services.AddSingleton(jwtSettings);
```

with:

```csharp
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
```

Leave `AddAppAuthentication` (line 178) reading the section eagerly via `Get<JwtSettings>()` - JwtBearer token validation parameters must be built at registration time.

Consumer migrations (a grep confirmed the old singleton had **no** consumers; the real consumers read raw `IConfiguration` and are migrated to typed options here):

**`src\ThePredictions.Infrastructure\Services\AuthenticationTokenService.cs`** - change the primary constructor from:

```csharp
public class AuthenticationTokenService(IUserManager userManager, IConfiguration configuration, IRefreshTokenRepository refreshTokenRepository, IDateTimeProvider dateTimeProvider) : IAuthenticationTokenService
```

to:

```csharp
public class AuthenticationTokenService(IUserManager userManager, IOptions<JwtSettings> jwtOptions, IRefreshTokenRepository refreshTokenRepository, IDateTimeProvider dateTimeProvider) : IAuthenticationTokenService
```

and inside `GenerateTokensAsync` replace the section reads (current lines 30-34 and 50):

```csharp
        var jwtSettings = configuration.GetSection("JwtSettings");
        var expiryMinutes = double.Parse(jwtSettings["ExpiryMinutes"]!);
        ...
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
```

with typed access:

```csharp
        var jwtSettings = jwtOptions.Value;
        var expiresAt = dateTimeProvider.UtcNow.AddMinutes(jwtSettings.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
```

with `issuer: jwtSettings.Issuer`, `audience: jwtSettings.Audience`, and `Expires = dateTimeProvider.UtcNow.AddDays(jwtSettings.RefreshTokenExpiryDays)`. Add `using Microsoft.Extensions.Options;` and `using ThePredictions.Infrastructure.Authentication.Settings;`, remove the now-unused `Microsoft.Extensions.Configuration` using. All four `double.Parse` calls disappear.

**`src\ThePredictions.API\Controllers\AuthControllerBase.cs`** - change:

```csharp
public abstract class AuthControllerBase(IConfiguration configuration) : ApiControllerBase
```

to:

```csharp
public abstract class AuthControllerBase(IOptions<JwtSettings> jwtSettings) : ApiControllerBase
```

and in `SetTokenCookie` replace:

```csharp
        var expiryDays = double.Parse(configuration["JwtSettings:RefreshTokenExpiryDays"]!);
```

with:

```csharp
        var expiryDays = jwtSettings.Value.RefreshTokenExpiryDays;
```

(This also resolves the culture-sensitive-parse bullet in audit item 4.8.)

**Derived controllers** (both only used `IConfiguration` to feed the base class - verified by grep):

- `src\ThePredictions.API\Controllers\AuthenticationController.cs` line 24: `AuthenticationController(ILogger<AuthenticationController> logger, IConfiguration configuration, IMediator mediator) : AuthControllerBase(configuration)` becomes `AuthenticationController(ILogger<AuthenticationController> logger, IOptions<JwtSettings> jwtSettings, IMediator mediator) : AuthControllerBase(jwtSettings)`.
- `src\ThePredictions.API\Controllers\ExternalAuthController.cs` line 18: same substitution (`IConfiguration configuration` -> `IOptions<JwtSettings> jwtSettings`, base call updated).

Unit tests mock `IAuthenticationTokenService` (the interface), so no Application test changes are expected; if any test constructs these controllers directly, hand it `Options.Create(new JwtSettings { ... })`.

### Step 2.5: Remove the host-local options binding and duplicate AddControllers from the Web host

In `src\ThePredictions.Web\Program.cs` delete line 60 (`builder.Services.AddControllers();` - `AddApiServices` already registers controllers) and lines 65-70:

```csharp
builder.Services.Configure<BrevoSettings>(builder.Configuration.GetSection("Brevo"));
builder.Services.Configure<EmailDeliverySettings>(builder.Configuration.GetSection("EmailDelivery"));
builder.Services.Configure<FootballApiSettings>(builder.Configuration.GetSection("FootballApi"));
builder.Services.Configure<FootballApiResilienceSettings>(builder.Configuration.GetSection("FootballApi:Resilience"));
builder.Services.Configure<TimeoutSettings>(builder.Configuration.GetSection("Timeouts"));
builder.Services.Configure<SiteSettings>(options => options.BaseUrl = builder.Configuration["ApiBaseUrl"]);
```

Remove `using ThePredictions.Application.Configuration;` (line 6) if it becomes unused. The bindings now happen inside `AddApiServices -> AddApplicationServices`, so the standalone API host gets them too - that is the fix for the empty-`IOptions` problem.

### Step 2.6: Confirm section names against configuration

The section names moved verbatim from `Web\Program.cs` ("Brevo", "EmailDelivery", "FootballApi", "FootballApi:Resilience", "Timeouts", key "ApiBaseUrl"). Cross-check them against `src\ThePredictions.Web\appsettings.json` before committing; do not invent `SectionName` constants for classes that lack them as part of this plan.

### Step 2.7: Update ContainerValidationTests

In `tests\Unit\ThePredictions.Composition.Tests.Unit\ContainerValidationTests.cs` delete lines 66-72 (the comment starting "Options the host binds in Program.cs (outside the two Add* methods)" and the five `services.Configure<...>` calls) - `AddApiServices` now performs those bindings from the in-memory configuration. Leave the configuration dictionary untouched. Update the class-level XML doc comment if it still claims options are bound outside the `Add*` methods.

### Step 2.8: Verify

Run the standard three commands (build with warnings-as-errors, composition test, full test suite). Then smoke-run both hosts: `dotnet run --project src/ThePredictions.Web` and `dotnet run --project src/ThePredictions.API` - both must start (the Development host validates the container on build).

---

## Phase 3: Shared host pipeline extension in ThePredictions.Hosting.Shared

Goal: the request pipeline that must be identical in both hosts lives in one method so the hosts cannot drift again. `src\ThePredictions.Hosting.Shared` currently contains only `Extensions\ConfigurationSubstitutionExtensions.cs`.

Ordering ground truth (verify before editing; preserve the **Web host's** order for the shared spine):

| Web host (`Web\Program.cs`) | API host (`API\Program.cs`) | Classification |
|---|---|---|
| UseForwardedHeaders (80) | - | Web-specific (behind proxy) |
| apex-to-www redirect (89) | - | Web-specific |
| UseWebAssemblyDebugging / UseExceptionHandler (106-115) | - | Web-specific |
| - | UseSwagger/UseSwaggerUI (dev, 12-16) | API-specific |
| UseSerilogRequestLogging (117) | - | Web-specific |
| CorrelationIdMiddleware (129) | (18) | **Shared** |
| ErrorHandlingMiddleware (130) | (19) | **Shared** |
| UseSecurityHeaders (added Phase 1) | (20) | **Shared** (host-specific CSP) |
| UseHttpsRedirection (131) | (22) | **Shared** |
| UseCors (132) | - | Web-specific slot |
| UseBlazorFrameworkFiles (133) | - | Web-specific slot |
| cache-control middleware (138) | - | Web-specific slot |
| UseStaticFiles (159) | - | Web-specific slot |
| UseCookiePolicy (160) | - | Web-specific slot |
| UseRouting (161) | (23) | **Shared** |
| UseRateLimiter (162) | (24) | **Shared** |
| UseAuthentication (163) | (25) | **Shared** |
| UseAuthorization (164) | (26) | **Shared** |
| MapControllers (165) | (27) | **Shared** |
| MapHealthCheckEndpoints (166) | (28) | **Shared** |
| MapFallbackToFile (167) | - | Web-specific |

The Web-specific slot sits between `UseHttpsRedirection` and `UseRouting`, so the shared extension takes a callback for it.

### Step 3.1: Beef up the Hosting.Shared project

`src\ThePredictions.Hosting.Shared\ThePredictions.Hosting.Shared.csproj` currently has only `Microsoft.Extensions.Configuration.Abstractions`. Add:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ThePredictions.Application\ThePredictions.Application.csproj" />
    <ProjectReference Include="..\ThePredictions.Infrastructure\ThePredictions.Infrastructure.csproj" />
  </ItemGroup>
```

Dependency check (no cycles): Hosting.Shared -> Infrastructure -> Application; API -> Hosting.Shared (existing reference, `API.csproj` lines 26-28, which becomes genuinely used - the `TreatAsUsed` metadata can be dropped). Serilog.AspNetCore 8.0.3 matches Infrastructure's existing pin; it is needed because `CorrelationIdMiddleware` uses `Serilog.Context.LogContext`. The Infrastructure reference is needed because the endpoint half of the pipeline calls `MapHealthCheckEndpoints()` from `ThePredictions.Infrastructure.HealthChecks.HealthCheckEndpointExtensions`.

### Step 3.2: Move the middleware

Move these files from `src\ThePredictions.API\Middleware\` to `src\ThePredictions.Hosting.Shared\Middleware\`, changing each namespace from `ThePredictions.API.Middleware` to `ThePredictions.Hosting.Shared.Middleware`:

- `CorrelationIdMiddleware.cs` (its `internal const` fields are only used inside the file itself - verified by grep - so the assembly change is safe)
- `ErrorHandlingMiddleware.cs` (compiles in Hosting.Shared because it needs Application/Domain exception types and FluentValidation, all reachable via the new Application reference)
- `SecurityHeadersMiddleware.cs`, `SecurityHeadersOptions.cs`, `SecurityHeadersMiddlewareExtensions.cs` (from Phase 1)

Then update the stale path references in `docs\security\accepted-risks.md` (lines 18 and 30 point at `src/ThePredictions.API/Middleware/...`).

### Step 3.3: Create the shared pipeline extension

Create `src\ThePredictions.Hosting.Shared\Extensions\SharedPipelineExtensions.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using ThePredictions.Hosting.Shared.Middleware;
using ThePredictions.Infrastructure.HealthChecks;

namespace ThePredictions.Hosting.Shared.Extensions;

/// <summary>
/// The request pipeline both hosts must share, in the exact order the production Web host
/// established. Host-specific static-content middleware (CORS, Blazor framework files,
/// static files) is injected via <paramref name="configureStaticContent"/> between
/// UseHttpsRedirection and UseRouting.
/// </summary>
public static class SharedPipelineExtensions
{
    public static WebApplication UseSharedApiPipeline(
        this WebApplication app,
        SecurityHeadersOptions? securityHeaders = null,
        Action<IApplicationBuilder>? configureStaticContent = null)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseSecurityHeaders(securityHeaders ?? new SecurityHeadersOptions());
        app.UseHttpsRedirection();

        configureStaticContent?.Invoke(app);

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthCheckEndpoints();

        return app;
    }
}
```

### Step 3.4: Rewrite the API host on the shared pipeline

`src\ThePredictions.API\Program.cs` becomes:

```csharp
using ThePredictions.API;
using ThePredictions.Hosting.Shared.Extensions;
using ThePredictions.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSharedApiPipeline();

app.Run();
```

### Step 3.5: Rewrite the Web host on the shared pipeline

In `src\ThePredictions.Web\Program.cs`, everything from the current line 129 (`app.UseMiddleware<CorrelationIdMiddleware>();`) down to line 166 (`app.MapHealthCheckEndpoints();`) is replaced by one call; everything above (forwarded headers, apex redirect, WASM debugging / exception handler, Serilog request logging) and the trailing `app.MapFallbackToFile("index.html");` stay put:

```csharp
app.UseSharedApiPipeline(
    securityHeaders: new SecurityHeadersOptions
    {
        ContentSecurityPolicy = /* the exact Web CSP string introduced in Phase 1 */
    },
    configureStaticContent: staticContent =>
    {
        staticContent.UseCors(corsName);
        staticContent.UseBlazorFrameworkFiles();

        // Prevent browser caching of index.html and blazor.boot.json (moved verbatim
        // from the previous inline middleware, including its comment block).
        staticContent.Use(async (context, next) =>
        {
            // ... existing OnStarting cache-control middleware body, unchanged ...
            await next();
        });

        staticContent.UseStaticFiles();
        staticContent.UseCookiePolicy();
    });

app.MapFallbackToFile("index.html");

app.Run();
```

Move the middleware bodies verbatim - do not rewrite the cache-control logic. Update the usings: replace `using ThePredictions.API.Middleware;` with `using ThePredictions.Hosting.Shared.Middleware;` (the `ThePredictions.Hosting.Shared.Extensions` using already exists for `EnableSubstitutions`). Confirm the resulting middleware order matches the table above exactly.

### Step 3.6: Verify

Standard three commands, then run both hosts and re-do the Phase 1 header checks against the Web host (`curl -kI`), plus hit `/health/ready` on both hosts and a Swagger page on the API host in Development.

---

## Phase 4: Package and reference hygiene

### Step 4.1: WebAssembly hosting packages move to the Web host

1. In `src\ThePredictions.Web\ThePredictions.Web.csproj`, add to the existing `ItemGroup` with package references:

```xml
        <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="8.0.14" />
```

2. In `src\ThePredictions.Infrastructure\ThePredictions.Infrastructure.csproj`, delete lines 17-20:

```xml
        <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="8.0.14" />
        <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="8.0.14">
          <TreatAsUsed>true</TreatAsUsed>
        </PackageReference>
```

3. In `src\ThePredictions.API\ThePredictions.API.csproj`, delete line 15:

```xml
        <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="8.0.14" />
```

`ThePredictions.Web.Client` keeps its own DevServer reference (`Web.Client.csproj` line 16) - that one is legitimate (it is what serves the WASM project when run standalone). `UseWebAssemblyDebugging()` and `UseBlazorFrameworkFiles()` both live in the `WebAssembly.Server` package, now referenced directly by the project that calls them.

### Step 4.2: Remove the dead Validators reference from Application

In `src\ThePredictions.Application\ThePredictions.Application.csproj`, delete line 28:

```xml
    <ProjectReference Include="..\ThePredictions.Validators\ThePredictions.Validators.csproj" />
```

Pre-verified safety: no `ThePredictions.Validators` usage exists anywhere in Application source, and the API (which registers the validators via `AddValidatorsFromAssemblyContaining<LoginRequestValidator>` in its own DI class) holds its own direct reference at `API.csproj` line 30, as do Web and Web.Client. Re-run the grep before deleting in case new code arrived:

```
grep -r "ThePredictions.Validators" src/ThePredictions.Application --include=*.cs
```

### Step 4.3: Give Web a direct Hosting.Shared reference

In `src\ThePredictions.Web\ThePredictions.Web.csproj`, add:

```xml
    <ProjectReference Include="..\ThePredictions.Hosting.Shared\ThePredictions.Hosting.Shared.csproj" />
```

`Web\Program.cs` calls `EnableSubstitutions()` (line 43) and, after Phase 3, `UseSharedApiPipeline()`; depending on those through the transitive API reference was fragile.

### Step 4.4: Verify

Standard three commands, then prove the Web host still serves the WASM client after the package moves:

1. `dotnet run --project src/ThePredictions.Web` (Local environment) - the app must boot in a browser (the Blazor loading screen must be replaced by the real UI, proving `_framework` assets are served).
2. `dotnet publish src/ThePredictions.Web/ThePredictions.Web.csproj -c Release -o ./publish-test` - confirm `publish-test/wwwroot/_framework/blazor.boot.json` exists, then delete `./publish-test`.

---

## Follow-ups (recorded here, not part of this plan)

- **Settings class convention:** target rule is "the options class lives in the layer that owns the interface consuming it". Under that rule `TimeoutSettings` and `FootballApiResilienceSettings` (consumed only by Infrastructure) would move from `Application\Configuration\` to Infrastructure, and `GoogleAuthSettings` (consumed only by API registration code) would move to the API project. None of this plan's steps touch those files' locations, so they stay put; move them only in a change that already touches them.
- **CSP hardening:** move the two inline `<script>` blocks out of `index.html` into a static `.js` file (or add CSP hashes) so `'unsafe-inline'` can be dropped from the Web host's `script-src`.
- **Self-hosting the CDN assets** (Bootstrap, icons, SweetAlert2, fonts) would let the CSP shrink back towards the API default and remove the jsdelivr runtime dependency.

## Out of scope

- Any behaviour change to authentication, rate limiting, CORS policy or health checks - registrations and middleware move, their configuration values do not change.
- The remaining code-consistency-audit items (except the three explicitly subsumed/resolved above).
- Merging the two hosts into one project, or deleting the standalone API host.
- Moving settings classes between layers (see Follow-ups).
- Adding new options validation (`ValidateDataAnnotations` / `ValidateOnStart`).
- Build tooling (`Directory.Build.props`, analysers) - see [`../build-tooling/README.md`](../build-tooling/README.md).

## Verification checklist

- [ ] Phase 1: `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` clean
- [ ] Phase 1: Web host responses carry CSP, X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy (and HSTS over HTTPS, emitted once, not twice)
- [ ] Phase 1: no CSP violations in the browser console on home, login (incl. Google), dashboard (external league logos), a SweetAlert2 modal, an admin page
- [ ] Phase 2: `ThePredictions.Application\DependencyInjection.cs` exists; Infrastructure DI no longer registers any `ThePredictions.Application.*` or `ThePredictions.Domain.Services` types; API DI no longer registers MediatR or prize services
- [ ] Phase 2: no `services.Configure<...>` options-binding lines remain in `Web\Program.cs`; standalone API host starts and `/health/ready` responds
- [ ] Phase 2: `JwtSettings` is registered only via `services.Configure<JwtSettings>`; no `double.Parse` of Jwt values remains; both auth controllers compile with `IOptions<JwtSettings>`
- [ ] Phase 2: `ContainerValidationTests` updated (redundant `Configure` block removed) and green
- [ ] Phase 3: both `Program.cs` files call `UseSharedApiPipeline`; middleware order matches the table; `docs\security\accepted-risks.md` paths updated
- [ ] Phase 4: `WebAssembly.DevServer` absent from API and Infrastructure csproj; `WebAssembly.Server` present only in Web csproj; Application csproj has no Validators reference; Web csproj references Hosting.Shared directly
- [ ] Phase 4: Web host serves the WASM client locally and `dotnet publish` output contains `_framework/blazor.boot.json`
- [ ] After every phase: `dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit/ThePredictions.Composition.Tests.Unit.csproj` green
- [ ] Final: `dotnet test ThePredictions.sln` fully green
