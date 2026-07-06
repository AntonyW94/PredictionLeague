# Refresh Tokens in URL Parameters

## Status

**Not Started** | In Progress | Complete

Previously **Deferred** (accepted risk for mobile compatibility) - un-deferred in July 2026 with a design that keeps the mobile-safe cookie mechanics unchanged. See History below.

## Summary

During the Google OAuth callback, the 30-day refresh token is passed to the Blazor client in the URL query string. This plan replaces the raw token in the URL with a short-lived (60-second), effectively single-use exchange code, while keeping every part of the flow that makes mobile login work exactly as it is today.

## Priority

**High** - the raw token in the URL is the only remaining place a long-lived credential is exposed to browser history, proxy logs, and the server request log, and the fix is small and low-risk.

## Severity

**Medium** - Token Exposure

## CWE Reference

CWE-598 (Information Exposure Through Query Strings)

## OWASP Reference

A04:2021 - Insecure Design

## History (do not delete - decision record)

- **January 2026:** Deferred as an accepted risk. Gemini-assisted analysis at build time concluded that mobile browsers (especially iOS Safari) could not reliably receive the refresh cookie during the OAuth redirect, so the token had to travel in the URL. Recorded in [`docs/security/accepted-risks.md`](../../../security/accepted-risks.md) section 5.
- **July 2026:** Architecture review found the premise was only half true. Mobile browsers do restrict cookies set on the cross-site redirect hop, but the current implementation does not rely on that hop anyway: the cookie is set later, on a same-origin POST from the callback page (see "Why this is mobile-safe" below). The URL is only a hand-off channel, so it does not need to carry the token itself - a worthless-after-60-seconds code works identically. Un-deferred.

## Problem Description

### Current flow (verified July 2026)

1. User clicks "Login with Google"; `GET external-auth/google-login` issues the OAuth challenge ([`ExternalAuthController.cs:26-48`](../../../../src/ThePredictions.API/Controllers/ExternalAuthController.cs)).
2. Google redirects back to `GET external-auth/signin-google`. On success the controller redirects to the client callback page **with the raw refresh token in the query string**:

   ```csharp
   // ExternalAuthController.cs lines 83-85
   case SuccessfulAuthenticationResponse success:
       var encodedToken = WebUtility.UrlEncode(success.RefreshTokenForCookie);
       return Redirect($"{returnUrl}?refreshToken={encodedToken}&source={source}");
   ```

3. The Blazor page `/authentication/external-login-callback` ([`ExternalLoginCallback.razor`](../../../../src/ThePredictions.Web.Client/Components/Pages/Authentication/ExternalLoginCallback.razor)) reads `RefreshToken` from the query string and calls `ApiAuthenticationStateProvider.LoginWithRefreshToken(...)`.
4. `LoginWithRefreshToken` ([`ApiAuthenticationStateProvider.cs:96-133`](../../../../src/ThePredictions.Web.Client/Authentication/ApiAuthenticationStateProvider.cs)) POSTs the token in the body to `api/authentication/refresh-token`. That endpoint rotates the token, **sets the refresh cookie on the POST response** via `SetTokenCookie` ([`AuthenticationController.cs:174`](../../../../src/ThePredictions.API/Controllers/AuthenticationController.cs)), and returns the access token in the body, which the client stores in localStorage.

### Why the URL exposure matters

- The token lands in browser history, any intermediary/proxy logs, and the Serilog request log for the follow-up GET.
- Worst case: if the callback URL leaks but is **never redeemed** (user abandons the navigation, URL copied from a log), the leaked refresh token is valid for its full 30-day life, because rotation only happens on use. The "single-use" mitigation recorded in January only holds when the callback page actually runs.

### Why this is mobile-safe to fix (the key insight)

The refresh cookie is **not** set on the OAuth redirect (step 2). It is set on the same-origin POST in step 4 - exactly the same mechanism as normal email/password login, which is proven to work on mobile. The mobile cookie restrictions apply to the redirect hop arriving from `google.com`; they do not apply to a first-party XHR the app makes to its own origin.

Therefore the URL parameter only needs to carry **something the callback page can trade for tokens**, not the token itself. Replacing the raw refresh token with a short-lived opaque exchange code changes nothing about redirects, cookies, or timing. The only delta on any browser, mobile included, is the meaning of the query-string value.

This is strictly better than today even before the history/referrer concerns: an unredeemed exchange code dies after 60 seconds, whereas an unredeemed leaked refresh token today lives for 30 days.

## Implementation Plan

No database changes. No new NuGet packages. ASP.NET Core Data Protection is used to make the code opaque and tamper-proof; the Web host already configures key persistence (`src/ThePredictions.Web/Program.cs` line 58: `AddDataProtection().PersistKeysToFileSystem(...)`), and `IDataProtectionProvider` is available from the framework by default in the standalone API host too.

### Step 1 - Contracts: exchange request DTO

Create `src/ThePredictions.Contracts/Authentication/ExchangeExternalLoginCodeRequest.cs`:

```csharp
namespace ThePredictions.Contracts.Authentication;

public class ExchangeExternalLoginCodeRequest
{
    public string Code { get; set; } = string.Empty;
}
```

(Match the property style of the existing `RefreshTokenRequest` in the same folder - read it first and mirror it exactly.)

### Step 2 - API: issue the code instead of the token

In [`ExternalAuthController.cs`](../../../../src/ThePredictions.API/Controllers/ExternalAuthController.cs):

1. Inject `IDataProtectionProvider dataProtectionProvider` and `IDateTimeProvider dateTimeProvider` (namespace `ThePredictions.Domain.Common`; it is registered as a singleton in `src/ThePredictions.Infrastructure/DependencyInjection.cs`) via the primary constructor, alongside the existing `logger`, `mediator`, `configuration`.
2. Add constants and a helper at the bottom of the controller (private methods, not new public types):

   ```csharp
   private const string ExchangeProtectorPurpose = "ExternalLoginTokenExchange";
   private static readonly TimeSpan ExchangeCodeLifetime = TimeSpan.FromSeconds(60);

   private string CreateExchangeCode(string refreshToken)
   {
       var protector = dataProtectionProvider.CreateProtector(ExchangeProtectorPurpose);
       return protector.Protect($"{dateTimeProvider.UtcNow.Ticks}|{refreshToken}");
   }
   ```

   Notes for the implementer:
   - `IDataProtector.Protect(string)` returns a base64url string (URL-safe; no `+` characters, so the `Replace(' ', '+')` workaround needed for raw refresh tokens does not apply).
   - Do NOT use `ITimeLimitedDataProtector` - it needs the extra `Microsoft.AspNetCore.DataProtection.Extensions` package; the embedded-ticks check below does the same job without a new dependency.
   - Check the actual name of the `UtcNow` property on `IDateTimeProvider` (`src/ThePredictions.Domain/Common/IDateTimeProvider.cs`) and use whatever it is.
3. Change the success arm of `GoogleCallbackAsync` (currently lines 83-85 quoted above) to:

   ```csharp
   case SuccessfulAuthenticationResponse success:
       var code = CreateExchangeCode(success.RefreshTokenForCookie);
       return Redirect($"{returnUrl}?code={Uri.EscapeDataString(code)}&source={source}");
   ```

4. Update the `[SwaggerOperation]`/`[SwaggerResponse]` text on both endpoints so they no longer claim tokens are passed in the redirect (they now say "with a short-lived exchange code").

### Step 3 - API: the exchange endpoint

Add to `ExternalAuthController` (it already carries `[EnableRateLimiting("auth")]` at class level, which is appropriate - one exchange per login):

```csharp
[HttpPost("exchange")]
[AllowAnonymous]
[SwaggerOperation(
    Summary = "Exchange a one-time external-login code for tokens",
    Description = "Called by the client callback page after Google login. Validates the short-lived code issued by the OAuth callback, rotates the underlying refresh token, sets the refresh token cookie, and returns an access token. Codes expire after 60 seconds and are invalidated by the rotation on first use.")]
[SwaggerResponse(200, "Exchange successful - returns access token and user details", typeof(AuthenticationResponse))]
[SwaggerResponse(400, "Code invalid, expired, or already used")]
public async Task<IActionResult> ExchangeAsync(
    [FromBody] ExchangeExternalLoginCodeRequest request,
    CancellationToken cancellationToken)
{
    string payload;
    try
    {
        var protector = dataProtectionProvider.CreateProtector(ExchangeProtectorPurpose);
        payload = protector.Unprotect(request.Code);
    }
    catch (CryptographicException)
    {
        logger.LogInformation("External login exchange code failed to unprotect; the client will be asked to log in again.");
        return BadRequest(new { message = "Invalid or expired login code." });
    }

    var separatorIndex = payload.IndexOf('|');
    if (separatorIndex <= 0 || !long.TryParse(payload[..separatorIndex], out var issuedAtTicks))
        return BadRequest(new { message = "Invalid or expired login code." });

    var issuedAtUtc = new DateTime(issuedAtTicks, DateTimeKind.Utc);
    if (dateTimeProvider.UtcNow - issuedAtUtc > ExchangeCodeLifetime)
    {
        logger.LogInformation("External login exchange code expired.");
        return BadRequest(new { message = "Invalid or expired login code." });
    }

    var refreshToken = payload[(separatorIndex + 1)..];
    var result = await mediator.Send(new RefreshTokenCommand(refreshToken), cancellationToken);

    if (result is not SuccessfulAuthenticationResponse success)
        return BadRequest(result);

    SetTokenCookie(success.RefreshTokenForCookie);
    return Ok(success);
}
```

Notes for the implementer:
- `CryptographicException` is `System.Security.Cryptography.CryptographicException`; add the using.
- `RefreshTokenCommand` lives at `src/ThePredictions.Application/Features/Authentication/Commands/RefreshToken/` and takes the token string (see its use at `AuthenticationController.cs:161`).
- The error body shape `{ message }` matches what the existing refresh endpoint returns on missing token (`AuthenticationController.cs:156`); keep it consistent. If the error-contract-standardisation plan ([`docs/todo/architecture/error-contract-standardisation/README.md`](../../architecture/error-contract-standardisation/README.md)) has landed first, use the standard shape it defines instead.
- Single-use semantics: the first exchange rotates (revokes) the refresh token inside `RefreshTokenCommandHandler` (`storedToken.Revoke(...)`, line 76). A replayed code presents a revoked token, which is rejected after the handler's 30-second multi-tab reuse grace window (`ReuseGraceWindow`, `RefreshTokenCommandHandler.cs:21`). The effective replay window is therefore max 60 seconds unredeemed, or ~30 seconds after redemption - identical to the grace semantics the refresh flow already accepts, and vastly smaller than today's 30-day worst case.

### Step 4 - Optional but recommended: validator

Add `src/ThePredictions.Validators/Authentication/ExchangeExternalLoginCodeRequestValidator.cs` with `RuleFor(x => x.Code).NotEmpty()`, matching the style of the existing authentication validators in that folder. (This only executes once the validation wiring from [`docs/todo/security/server-validation-gap/README.md`](../server-validation-gap/README.md) is in place; it is harmless before then.)

### Step 5 - Client: exchange instead of raw token

1. In [`ApiAuthenticationStateProvider.cs`](../../../../src/ThePredictions.Web.Client/Authentication/ApiAuthenticationStateProvider.cs), replace `LoginWithRefreshToken` (lines 96-133) with:

   ```csharp
   public async Task<bool> LoginWithExchangeCodeAsync(string code)
   {
       logger.LogInformation("Attempting to log in with external login exchange code.");

       if (string.IsNullOrEmpty(code))
       {
           logger.LogWarning("Exchange code from URL is null or empty.");
           return false;
       }

       var request = new ExchangeExternalLoginCodeRequest { Code = code };
       var response = await httpClient.PostAsJsonAsync("external-auth/exchange", request);

       if (!response.IsSuccessStatusCode)
       {
           logger.LogError("API call to exchange login code failed with status code: {StatusCode}", response.StatusCode);
           return false;
       }

       var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
       if (authResponse == null)
       {
           logger.LogError("Failed to deserialise successful authentication response.");
           return false;
       }

       await localStorage.SetItemAsync(AccessTokenKey, authResponse.AccessToken);
       httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authResponse.AccessToken);
       NotifyUserAuthentication();

       return true;
   }
   ```

   - Note the URL is `external-auth/exchange` (the controller route has no `api/` prefix). Verify the HttpClient `BaseAddress` used by this class resolves relative URLs from the site root (check `src/ThePredictions.Web.Client/Program.cs` / `DependencyInjection.cs`); the existing `LoginWithRefreshToken` posts to a relative URL with the same client, so mirror whatever works there.
   - The `refreshToken.Replace(' ', '+')` line from the old method is intentionally dropped: exchange codes are base64url and never contain `+`.
2. In [`ExternalLoginCallback.razor`](../../../../src/ThePredictions.Web.Client/Components/Pages/Authentication/ExternalLoginCallback.razor):
   - Replace `[SupplyParameterFromQuery] public string? RefreshToken { get; set; }` with `[SupplyParameterFromQuery] public string? Code { get; set; }`.
   - Call `LoginWithExchangeCodeAsync(Code)` instead of `LoginWithRefreshToken(RefreshToken)`.
   - Fix the failure log message at line 45 (it references a `'token'` parameter): "FAILURE: 'code' query parameter NOT found in external login callback."
3. The skip-refresh guard in `CreateAuthenticationStateAsync` (`ApiAuthenticationStateProvider.cs:35-41`) keys off the callback path, not the parameter name - no change needed.
4. Check `tests/Unit/ThePredictions.Web.Client.Tests.Unit/` for tests referencing `LoginWithRefreshToken` (there is an `ApiAuthenticationStateProviderTests` file) and update them to the new method and endpoint.

### Step 6 - Deployment note

Server and client ship in the same deployment (the Web host serves both), so the change is atomic - no backwards-compatibility window is needed for the old `?refreshToken=` parameter. Do not keep a fallback path that still accepts a raw refresh token in the URL; that would defeat the fix.

### Step 7 - Documentation updates (same PR)

- Update [`docs/security/accepted-risks.md`](../../../security/accepted-risks.md) section 5 ("Refresh Tokens in URL Parameters"): mark it resolved by this plan and describe the new flow in one sentence.
- Update the flow description in this README's Problem Description to past tense, and flip Status to Complete.
- Check `docs/todo/security/README.md`'s deferred-items table row for this plan and move/reword it accordingly.

## Verification checklist

Build and unit tests first:

- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` clean
- [ ] `dotnet test tests/Unit/ThePredictions.Web.Client.Tests.Unit` green (updated auth provider tests)
- [ ] `dotnet test` for Validators if Step 4 was done

Then deploy to **dev** (`dev.thepredictions.co.uk`) and complete ALL of the following manual tests. **Google login on mobile is the whole reason the old design existed - do not merge to production until every mobile case below passes.**

- [ ] iOS Safari, normal mode: Google login lands on the dashboard
- [ ] iOS Safari, normal mode: after login, close the tab, reopen the site 20+ minutes later (access token expired) - still logged in (proves the refresh cookie was set)
- [ ] iOS Safari, private browsing: Google login works within the session
- [ ] Android Chrome, normal mode: both checks as per iOS
- [ ] Android Chrome, incognito: login works within the session
- [ ] Desktop Chrome, Firefox, and Edge: login works; URL bar/history shows only `?code=...`, never a token
- [ ] A browser with an ad blocker enabled: login works
- [ ] Error path: cancel the Google consent screen - lands back on the login page with the error message (unchanged behaviour)
- [ ] Replay: capture the `?code=` value from a completed login, wait 90 seconds, POST it to `external-auth/exchange` manually (curl) - expect 400
- [ ] Regression: email/password login, logout, and silent token refresh all still work on desktop and mobile
- [ ] Regression: register, then Google login with the same Google account (external-account linking path, if applicable) still works

## Out of scope

- Moving the access token out of localStorage ([`docs/todo/security/localstorage-tokens/README.md`](../localstorage-tokens/README.md) - separately deferred).
- JWT hardening items ([`docs/todo/security/jwt-security-hardening/README.md`](../jwt-security-hardening/README.md)).
- The `RefreshTokenCommandHandler` reuse grace window design (existing, deliberate).
- Standardising the ExternalAuthController route prefix (noted controller-convention drift; separate concern).

## Review trigger (if this plan is ever re-deferred)

Re-review if mobile testing surfaces a browser where the same-origin POST cookie mechanism fails - but note that would equally break normal email/password login, which is not observed.
