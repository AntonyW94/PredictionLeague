# Origin Header Poisoning of Emailed Links

## Status

**Not Started** | In Progress | Complete

## Summary

Several API endpoints build the absolute URLs that go into outbound emails from the request's `Origin` HTTP header (`Request.Headers["Origin"]`). The `Origin` header is fully attacker-controllable on a direct API call, so an attacker can cause a security email (most seriously the password-reset email) to be sent to a victim with a link that points at an attacker-controlled host. When the victim clicks the link, the reset token is delivered to the attacker.

The fix is to stop trusting the `Origin` header for link construction and instead derive the site base URL from configuration (`SiteSettings.BaseUrl`, which already exists and is bound per environment). At the same time we consolidate the six Application handlers that each hardcode the same `"https://www.thepredictions.co.uk"` fallback into a single `SiteSettings.ResolvedBaseUrl` helper (already planned as item 4.7 of the June 2026 code review), and route every controller site and every handler site through it.

## Priority

**High** - attacker-controllable links in security emails. The password-reset path (`forgot-password`) hands a live reset token to whatever host the `Origin` header names, which is a direct account-takeover vector.

## Severity

**High** - P1. Account takeover via password-reset link poisoning. The confirm-email and league-notification links are lower impact (confirmation token / dashboard deep-links) but share the same root cause and are fixed together.

## CWE / OWASP References

- **CWE-640: Weak Password Recovery Mechanism for Forgotten Password** - the classic reference for reset-link poisoning; the recovery flow emits a token-bearing link whose host is not trusted.
- **CWE-451: User Interface (UI) Misrepresentation of Critical Information** - the emailed link presents an attacker host as if it were the legitimate site.
- Related: host / `Origin` header injection is commonly discussed as "host header poisoning"; see also **CWE-20 (Improper Input Validation)** for the general failure to validate the inbound header.
- **OWASP:** A07:2021 - Identification and Authentication Failures (password recovery), with an element of A01:2021 - Broken Access Control (token reaches an unauthorised party).

## Problem Description

### The attacker-controllable input

`Origin` is a request header. On a browser-issued request it is set by the browser, but on a **direct API call** (curl, a script, Burp, etc.) the caller sets it to anything. None of the sites below validate it against an allow-list before embedding it in an email body.

### Confirmed sites

All paths verified by reading the files.

**`src/ThePredictions.API/Controllers/AuthenticationController.cs`**

- Line 42, `RegisterAsync`:
  ```csharp
  var confirmUrlBase = $"{Request.Headers["Origin"]}/authentication/confirm-email";
  ```
  Feeds `RegisterCommand.ConfirmUrlBase` -> `EmailConfirmationSender.SendAsync`, which builds `{confirmUrlBase}?token={token}` for the **email-confirmation** email.
- Line 87, `ResendConfirmationAsync`:
  ```csharp
  var confirmUrlBase = $"{Request.Headers["Origin"]}/authentication/confirm-email";
  ```
  Feeds `ResendConfirmationCommand.ConfirmUrlBase` -> same confirmation sender (**resend confirmation** email).
- Line 212, `ForgotPasswordAsync`:
  ```csharp
  var resetUrlBase = $"{Request.Headers["Origin"]}/authentication/reset-password";
  ```
  Feeds `RequestPasswordResetCommand.ResetUrlBase` -> `RequestPasswordResetCommandHandler`, which builds `{resetUrlBase}?token={resetToken.Token}` for the **password-reset** email (and derives the login-link base for the Google-user variant by string-replacing `/authentication/reset-password`). **This is the account-takeover vector.**

**`src/ThePredictions.API/Controllers/LeaguesController.cs`**

- Line 428, `UpdateLeagueAsync`:
  ```csharp
  LeagueUrlBase: Request.Headers["Origin"].ToString());
  ```
  Sets `UpdateLeagueCommand.LeagueUrlBase`. When toggling approval off auto-approves waiting members, the handler forwards it to `NotifyMemberOfLeagueApprovalCommand`, which builds the **"you can now take part"** email's `LEAGUE_URL` (`{base}/leagues/{id}/dashboard`).
- Line 508, `JoinLeagueAsync` (entry-code join):
  ```csharp
  var command = new JoinLeagueCommand(CurrentUserId, CurrentUserFirstName, CurrentUserLastName, null, request.EntryCode, Request.Headers["Origin"].ToString());
  ```
  Sets `JoinLeagueCommand.LeagueUrlBase`. `JoinLeagueCommandHandler` forwards it to either `NotifyMemberOfLeagueApprovalCommand` (auto-approved: **approval** email) or `NotifyLeagueAdminOfJoinRequestCommand` (pending: **admin "someone wants to join"** email, `DASHBOARD_URL` = `{base}/dashboard?tab=admin`).
- Line 526, `JoinPublicLeagueAsync` (direct public join): same as above with `leagueId` supplied.
- Line 547, `UpdateLeagueMemberStatusAsync`:
  ```csharp
  var command = new UpdateLeagueMemberStatusCommand(leagueId, memberId, CurrentUserId, newStatus, Request.Headers["Origin"].ToString());
  ```
  Sets `UpdateLeagueMemberStatusCommand.LeagueUrlBase`; on approval the handler forwards it to `NotifyMemberOfLeagueApprovalCommand` (**approval** email).

### Why enumeration resistance does not help

`ForgotPasswordAsync` already returns a constant 200 regardless of whether the account exists (anti-enumeration). That protects the *existence* of the account, not the *destination* of the link. An attacker who already knows a victim's email submits a forgot-password request with `Origin: https://evil.example`, and the victim receives a genuine-looking email whose reset link carries a valid token to the attacker's host. The rate limiter (`[EnableRateLimiting("auth")]`) also does not mitigate this; one request is enough.

### The duplicated fallback (June review item 4.7)

Six Application handlers already read `SiteSettings.BaseUrl` (or a passed-in URL base) and each repeat the same fallback-and-trim expression. Verified:

- `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendPrizeNotificationsCommandHandler.cs` (lines 51-53)
- `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendRoundDigestEmailsCommandHandler.cs` (lines 57-59)
- `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs` (lines 59-61)
- `src/ThePredictions.Application/Features/External/Tasks/Commands/SendLeagueWelcomeEmailsCommandHandler.cs` (lines 46-48)
- `src/ThePredictions.Application/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandler.cs` (lines 58-60, on the passed-in `leagueUrlBase`)
- `src/ThePredictions.Application/Features/Leagues/Commands/NotifyMemberOfLeagueApprovalCommandHandler.cs` (lines 61-63, on the passed-in `leagueUrlBase`)

Each is a variant of:

```csharp
var baseUrl = string.IsNullOrWhiteSpace(_siteSettings.BaseUrl)
    ? "https://www.thepredictions.co.uk"
    : _siteSettings.BaseUrl.TrimEnd('/');
```

Two further sites hold the same fallback string as a constant: `GetEmailTestDefaultsQueryHandler` (`FallbackBaseUrl`) and `SendLeagueWelcomeEmailsCommandHandler.CanonicalImageBaseUrl` (the latter is a deliberate *image* CDN base, not a link base - leave it alone). June review item 4.7 records the plan: "Site base-URL fallback string appears five times -> add `SiteSettings.ResolvedBaseUrl`". This plan delivers that helper and applies it everywhere.

### Configuration - how each environment already produces the right host

`SiteSettings.BaseUrl` is bound in `src/ThePredictions.Web/Program.cs` line 70:

```csharp
builder.Services.Configure<SiteSettings>(options => options.BaseUrl = builder.Configuration["ApiBaseUrl"]);
```

`ApiBaseUrl` differs per environment (never a secret - these are public site URLs):

| File | `ApiBaseUrl` |
|------|--------------|
| `src/ThePredictions.Web/appsettings.json` | `""` (empty base; overridden per environment) |
| `src/ThePredictions.Web/appsettings.Development.json` | `https://dev.thepredictions.co.uk` |
| `src/ThePredictions.Web/appsettings.Production.json` | `https://www.thepredictions.co.uk` |
| `src/ThePredictions.Web/appsettings.Local.json` | `https://localhost:7132` |

So a configured base URL preserves today's per-host behaviour: the **dev** deployment (`ASPNETCORE_ENVIRONMENT=Development`) emits `dev.thepredictions.co.uk` links, **production** emits `www.thepredictions.co.uk` links, and **local** development emits `localhost:7132` links - each because the deployment loads its own `appsettings.{Environment}.json`. The `Origin`-derived approach only appeared to "follow the user" because dev and prod are **separate deployments with separate appsettings**; a configured base gives the identical result without trusting the header. Verified: dev/prod/local each have distinct `appsettings` files with distinct `ApiBaseUrl` values.

### Host-binding caveat (coordination note)

`SiteSettings` is bound **only** in the Web host (`src/ThePredictions.Web/Program.cs` line 70). The standalone API host (`src/ThePredictions.API/Program.cs`) calls `AddInfrastructureServices` + `AddApiServices` and does **not** bind `SiteSettings` (confirmed: no `Configure<SiteSettings>` in `src/ThePredictions.API/DependencyInjection.cs` or `src/ThePredictions.Infrastructure/DependencyInjection.cs`). The deployed application runs the controllers inside the Web host, so this fix is effective there. If the standalone API host is ever used to serve these endpoints, `SiteSettings` must be bound there too. The parallel plan `docs/todo/architecture/composition-root-and-hosting/README.md` is expected to fix options binding for both hosts; this plan depends on that work for the standalone-API host and should be sequenced with it. Until then, add the `SiteSettings` binding to the API host as part of step 5 below so the fix cannot silently fall back to the hardcoded default.

## Implementation Plan

### Step 1 - add `SiteSettings.ResolvedBaseUrl`

Edit `src/ThePredictions.Application/Configuration/SiteSettings.cs`. Add a get-only property that returns the configured `BaseUrl` (trimmed of a trailing slash) or the canonical fallback when unset:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class SiteSettings
{
    // Public site root (e.g. https://www.thepredictions.co.uk). Used to build absolute links in emails
    // sent from background jobs, where there is no request Origin to derive the URL from.
    public string? BaseUrl { get; set; }

    // Canonical fallback used only when BaseUrl is not configured. Kept in one place so every email link
    // builder resolves the same value. Never derived from a request header (that is attacker-controllable).
    public const string FallbackBaseUrl = "https://www.thepredictions.co.uk";

    // The site root to build links from: the configured BaseUrl with any trailing slash removed, or the
    // fallback when BaseUrl is blank. Always returns a value with no trailing slash.
    public string ResolvedBaseUrl =>
        string.IsNullOrWhiteSpace(BaseUrl)
            ? FallbackBaseUrl
            : BaseUrl.TrimEnd('/');
}
```

Note: `SiteSettings` is a data-only class with a small amount of logic now (the property). It is in the Application project, so it is covered by the Application test project rather than the Domain 100% rule; add a unit test for `ResolvedBaseUrl` (step 8).

### Step 2 - use `ResolvedBaseUrl` in the six handlers

In each handler below, replace the inline fallback-and-trim block with `_siteSettings.ResolvedBaseUrl` (for the two Notify handlers, which take the base as a method argument, introduce `IOptions<SiteSettings>` and resolve from it - see step 3). Handlers that already inject `IOptions<SiteSettings>`:

- `SendPrizeNotificationsCommandHandler.cs` - replace lines 51-53 (`var baseUrl = string.IsNullOrWhiteSpace(_siteSettings.BaseUrl) ? ... : _siteSettings.BaseUrl.TrimEnd('/');`) with `var baseUrl = _siteSettings.ResolvedBaseUrl;`
- `SendRoundDigestEmailsCommandHandler.cs` - replace lines 57-59 the same way.
- `SendScheduledRemindersCommandHandler.cs` - replace lines 59-61 the same way.
- `SendLeagueWelcomeEmailsCommandHandler.cs` - replace lines 46-48 the same way. Leave `CanonicalImageBaseUrl` (line 111) unchanged - it is an image CDN base, not a link base.
- `GetEmailTestDefaultsQueryHandler.cs` - remove the private `FallbackBaseUrl` const (line 17) and replace lines 41-42 with `var baseUrl = siteSettings.Value.ResolvedBaseUrl;`.

### Step 3 - make the two Notify handlers resolve from configuration, not the passed-in base

The two Notify handlers currently receive `leagueUrlBase` (ultimately the poisoned `Origin`) as command data and fall back to the hardcoded string. Change them to resolve the base from `SiteSettings` and stop using the passed-in value.

`NotifyLeagueAdminOfJoinRequestCommandHandler.cs`:
- Add `IOptions<SiteSettings> siteSettings` to the primary constructor and `private readonly SiteSettings _siteSettings = siteSettings.Value;`.
- Change `BuildAdminDashboardUrl` to take no URL argument and use `_siteSettings.ResolvedBaseUrl`:
  ```csharp
  var parameters = new
  {
      // ...
      DASHBOARD_URL = $"{_siteSettings.ResolvedBaseUrl}/dashboard?tab=admin"
  };
  ```
  Remove the `BuildAdminDashboardUrl(string?)` helper (or make it parameterless).

`NotifyMemberOfLeagueApprovalCommandHandler.cs`:
- Add `IOptions<SiteSettings> siteSettings` + field as above.
- Change the link build to `LEAGUE_URL = $"{_siteSettings.ResolvedBaseUrl}/leagues/{request.LeagueId}/dashboard"` and remove the `BuildLeagueDashboardUrl` helper.

### Step 4 - remove `Origin` from the controllers and stop threading the URL base through commands

Now that link bases come from configuration, the `LeagueUrlBase` / `ConfirmUrlBase` / `ResetUrlBase` command fields are no longer needed for URL construction. Two acceptable approaches - choose the smaller diff:

**Preferred (remove the now-dead command fields):**

1. In `AuthenticationController.cs`:
   - `RegisterAsync` (lines 42-50): delete the `confirmUrlBase` local and the `confirmUrlBase` constructor argument; `RegisterCommand` drops its `ConfirmUrlBase` parameter. Update `RegisterCommandHandler` to build the confirm link from `SiteSettings.ResolvedBaseUrl` + `"/authentication/confirm-email"` (inject `IOptions<SiteSettings>` into `EmailConfirmationSender` or the handler, wherever the link is assembled).
   - `ResendConfirmationAsync` (lines 87-89): delete `confirmUrlBase`; `ResendConfirmationCommand` drops `ConfirmUrlBase`; the handler resolves the base the same way.
   - `ForgotPasswordAsync` (lines 210-214): delete `resetUrlBase`; `RequestPasswordResetCommand` drops `ResetUrlBase`; `RequestPasswordResetCommandHandler` builds `{ResolvedBaseUrl}/authentication/reset-password?token=...` and the Google-user login link from `{ResolvedBaseUrl}/authentication/login` (replacing the current `resetUrlBase.Replace("/authentication/reset-password", "")` string surgery).
2. In `LeaguesController.cs`, remove the `Request.Headers["Origin"].ToString()` arguments at lines 428, 508, 526, 547 and drop the corresponding `LeagueUrlBase` parameters from `UpdateLeagueCommand`, `JoinLeagueCommand`, and `UpdateLeagueMemberStatusCommand`; remove the `leagueUrlBase` threading in `JoinLeagueCommandHandler`, `UpdateLeagueCommandHandler`, and `UpdateLeagueMemberStatusCommandHandler` (they no longer pass it to the Notify commands).
3. Remove the `LeagueUrlBase` field from `NotifyLeagueAdminOfJoinRequestCommand` and `NotifyMemberOfLeagueApprovalCommand`.

**Minimal (keep the fields, stop populating from `Origin`):** if reducing churn is preferred, leave the command fields but pass a constant/empty value from the controllers and have handlers ignore it in favour of `ResolvedBaseUrl`. The preferred approach is cleaner and removes the dead data; use it unless the field removal ripples too far.

Either way, **no code path may read `Request.Headers["Origin"]` to build an emailed link** after this change. Verify with the search in the checklist.

### Step 5 - ensure `SiteSettings` is bound in every host that serves these endpoints

- The Web host already binds it (`src/ThePredictions.Web/Program.cs` line 70) - no change needed.
- Add the same binding to the standalone API host so the resolver never silently falls back:
  ```csharp
  // src/ThePredictions.API/Program.cs, after AddApiServices(...)
  builder.Services.Configure<SiteSettings>(options => options.BaseUrl = builder.Configuration["ApiBaseUrl"]);
  ```
  Add the corresponding `appsettings` keys to the API project if it does not already carry `ApiBaseUrl` for each environment. Coordinate with `docs/todo/architecture/composition-root-and-hosting/README.md`, which centralises options binding for both hosts; if that work lands first, this binding may already be present.

### Step 6 - build

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
```

Fix any compiler errors from the removed command parameters (constructor call sites) until the solution builds clean under `TreatWarningsAsErrors`.

### Step 7 - update existing handler tests affected by the signature changes

The handler unit tests construct commands directly, so any test that passed a `LeagueUrlBase` / `ConfirmUrlBase` / `ResetUrlBase` argument, or that asserted the forwarded `LeagueUrlBase` value, must be updated to the new signatures. Confirmed touch points:

- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Leagues/Commands/JoinLeagueCommandHandlerTests.cs` (line 183 constructs `JoinLeagueCommand(..., LeagueUrlBase: "https://www.thepredictions.co.uk")`; lines 191-198 assert `n.LeagueUrlBase == "https://www.thepredictions.co.uk"` on the forwarded `NotifyMemberOfLeagueApprovalCommand`). Remove the argument and the `LeagueUrlBase` assertion; assert instead that the notify command is sent.
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandlerTests.cs` and any `NotifyMemberOfLeagueApprovalCommandHandlerTests` (if added) must now inject `IOptions<SiteSettings>` and assert the link uses the resolved base (e.g. `https://test.local/dashboard?tab=admin`).
- `RegisterCommandHandlerTests`, `RequestPasswordResetCommandHandlerTests`, and any `ResendConfirmationCommandHandler` / `JoinLeagueCommandHandler` / `UpdateLeagueCommandHandler` / `UpdateLeagueMemberStatusCommandHandler` tests that reference the removed fields must be updated to the new constructor shape and, where they exercise link building, to assert the configured base is used.

Existing tests that already set `SiteSettings { BaseUrl = "https://test.local" }` (SendPrizeNotifications, SendRoundDigestEmails, SendLeagueWelcomeEmails, GetEmailTestDefaults) keep passing because `ResolvedBaseUrl` returns `https://test.local` for that input - no change to their expectations.

### Step 8 - add a unit test for `ResolvedBaseUrl`

Add `tests/Unit/ThePredictions.Application.Tests.Unit/Configuration/SiteSettingsTests.cs` (xUnit v3, FluentAssertions; no mocks needed):

```csharp
using FluentAssertions;
using ThePredictions.Application.Configuration;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Configuration;

public class SiteSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolvedBaseUrl_ShouldReturnFallback_WhenBaseUrlIsBlank(string? baseUrl)
    {
        var settings = new SiteSettings { BaseUrl = baseUrl };

        settings.ResolvedBaseUrl.Should().Be(SiteSettings.FallbackBaseUrl);
    }

    [Theory]
    [InlineData("https://dev.thepredictions.co.uk", "https://dev.thepredictions.co.uk")]
    [InlineData("https://dev.thepredictions.co.uk/", "https://dev.thepredictions.co.uk")]
    [InlineData("https://localhost:7132///", "https://localhost:7132")]
    public void ResolvedBaseUrl_ShouldReturnTrimmedConfiguredValue_WhenBaseUrlIsSet(string configured, string expected)
    {
        var settings = new SiteSettings { BaseUrl = configured };

        settings.ResolvedBaseUrl.Should().Be(expected);
    }
}
```

### Step 9 - run the tests

```
dotnet test tests/Unit/ThePredictions.Application.Tests.Unit/ThePredictions.Application.Tests.Unit.csproj
```

If the domain projects are also touched by the constructor changes, run the full unit set:

```
dotnet test ThePredictions.sln
```

## Out of scope

- The **image** base URL (`SendLeagueWelcomeEmailsCommandHandler.CanonicalImageBaseUrl`) - it is a deliberately fixed CDN host for email images, not a click-through link, and is not attacker-influenced.
- Introducing an `Origin` / host allow-list to *keep* using the header - rejected in favour of configuration, which is simpler and removes the trust in the header entirely.
- The apex-to-www canonical redirect middleware in `src/ThePredictions.Web/Program.cs` (unrelated to email links).
- The broader options-binding centralisation across both hosts - tracked in `docs/todo/architecture/composition-root-and-hosting/README.md`; this plan only adds the single `SiteSettings` binding needed for correctness.
- Server-side validation of the request DTOs - tracked in `docs/todo/security/server-validation-gap/README.md`.

## Verification checklist

- [ ] `src/ThePredictions.Application/Configuration/SiteSettings.cs` has `ResolvedBaseUrl` (trailing-slash-trimmed, fallback when blank) and a single `FallbackBaseUrl` constant.
- [ ] No handler contains an inline `string.IsNullOrWhiteSpace(... BaseUrl) ? "https://www.thepredictions.co.uk" : ...TrimEnd('/')` block; all use `ResolvedBaseUrl`.
- [ ] The two Notify handlers inject `IOptions<SiteSettings>` and build links from `ResolvedBaseUrl`, not from a passed-in URL base.
- [ ] No source file reads `Request.Headers["Origin"]` to build an emailed link. Confirm:
  ```
  rg -n "Headers\[\"Origin\"\]" src
  ```
  returns no results in `AuthenticationController.cs` or `LeaguesController.cs` (any remaining use must be unrelated to link building).
- [ ] The removed command parameters (`ConfirmUrlBase`, `ResetUrlBase`, `LeagueUrlBase`) have no remaining references:
  ```
  rg -n "ConfirmUrlBase|ResetUrlBase|LeagueUrlBase" src
  ```
- [ ] `SiteSettings` is bound in the standalone API host (`src/ThePredictions.API/Program.cs`) as well as the Web host.
- [ ] Solution builds clean: `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true`.
- [ ] `SiteSettingsTests` added and passing; all affected handler tests updated.
- [ ] `dotnet test tests/Unit/ThePredictions.Application.Tests.Unit/ThePredictions.Application.Tests.Unit.csproj` passes; `dotnet test ThePredictions.sln` passes.
- [ ] Manual check per environment (or reasoned from config): dev config yields `dev.thepredictions.co.uk` links, production yields `www.thepredictions.co.uk`, local yields `localhost:7132`, with a spoofed `Origin` header having no effect on the emailed link.
