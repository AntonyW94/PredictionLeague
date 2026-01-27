# Security Fixes Plan

## Overview

This document outlines the security vulnerabilities identified in the PredictionLeague application. The original audit was conducted January 24, 2026, with comprehensive follow-up audits on January 25 and January 27, 2026.

## Audit Dates
- **Initial Audit:** January 24, 2026
- **Comprehensive Follow-up:** January 25, 2026
- **Third Audit:** January 27, 2026

## Summary

| Priority | Count | Description |
|----------|-------|-------------|
| Completed | 28 | Fixes implemented and verified |
| Deferred | 4 | Require login system changes or architectural decisions |
| P0 - Critical | 0 | Fix immediately - active exploitation risk |
| P1 - High | 0 | Fix this sprint - significant security impact |
| P2 - Medium | 3 | Fix soon - defence in depth |
| Low | 4 | Minor improvements and housekeeping |

**Total Findings:** 39 (28 completed, 4 deferred, 7 outstanding)

---

## Already Fixed

**Completed plans moved to:** [`./completed/`](./completed/)

- [x] TasksController `[AllowAnonymous]` bypass
- [x] ErrorHandlingMiddleware not registered
- [x] XSS via user names (NameValidator added)
- [x] API Key Timing Attack - [03-api-key-timing-attack.md](./completed/03-api-key-timing-attack.md)
- [x] Rate Limiting - [07-rate-limiting.md](./completed/07-rate-limiting.md)
- [x] Security Headers - [08-security-headers.md](./completed/08-security-headers.md)
- [x] Handler Authorization - [10-handler-authorization.md](./completed/10-handler-authorization.md)
- [x] IDOR: Unauthorized League Update - [01-idor-league-update.md](./completed/01-idor-league-update.md)
- [x] Password Hash in DTO - [04-password-hash-disclosure.md](./completed/04-password-hash-disclosure.md)
- [x] IDOR: League Members Access - [05-idor-league-members.md](./completed/05-idor-league-members.md)
- [x] IDOR: Leaderboard Access - [06-idor-leaderboards.md](./completed/06-idor-leaderboards.md)
- [x] Missing Validators - [09-missing-validators.md](./completed/09-missing-validators.md)
- [x] Boost System Race Condition - [11-boost-race-condition.md](./completed/11-boost-race-condition.md)
- [x] IDOR: League Data Endpoints - [13-idor-league-data-endpoints.md](./completed/13-idor-league-data-endpoints.md)
- [x] Sensitive Data Logging - [14-sensitive-data-logging.md](./completed/14-sensitive-data-logging.md)
- [x] Boost Deadline Enforcement - [17-boost-deadline-enforcement.md](./completed/17-boost-deadline-enforcement.md)
- [x] Admin Command Validators - [19-admin-command-validators.md](./completed/19-admin-command-validators.md)
- [x] Configuration Hardening - [20-configuration-hardening.md](./completed/20-configuration-hardening.md)
- [x] JavaScript XSS Prevention - [21-javascript-xss-prevention.md](./completed/21-javascript-xss-prevention.md)
- [x] Login Password MaxLength (added to LoginRequestValidator)
- [x] League Name Character Validation (added LeagueNameValidationExtensions)
- [x] Access Token Expiry reduced from 60 to 15 minutes
- [x] brevo_csharp package verified at latest version (1.1.1)
- [x] Password Policy Configuration (ASP.NET Identity options in DependencyInjection.cs)
- [x] CORS Hardening (restricted methods and headers in Program.cs)
- [x] Lock Scoring Configuration (secured by design - scoring values immutable after creation)
- [x] ShortName Validation (added to BaseTeamRequestValidator)
- [x] Season Name Character Validation (added SafeNameValidationExtensions)
- [x] Rate Limiting Middleware Enabled - [22-rate-limiting-middleware.md](./completed/22-rate-limiting-middleware.md)
- [x] Outdated Packages Updated (.NET 10, JWT 8.15.0, framework packages 10.0.2) - [25-outdated-packages.md](./completed/25-outdated-packages.md)
- [x] IDOR: Round Results Access - [23-idor-round-results.md](./completed/23-idor-round-results.md)
- [x] Email Enumeration via Registration - [24-email-enumeration.md](./completed/24-email-enumeration.md)

## Intentionally Deferred

**Deferred plans moved to:** [`./later/`](./later/)

The following issues have been deferred due to mobile browser cookie compatibility constraints, architectural decisions, or require login system changes:
- Refresh tokens in URLs (ExternalAuthController) - mobile browser compatibility
- Access tokens in localStorage (Blazor WASM architectural decision) - XSS risk mitigated by CSP
- SameSite=None on Refresh Token Cookies - required for cross-site authentication flow
- JWT ClockSkew and Algorithm Whitelist - requires careful testing with production auth flows
- Open Redirect Vulnerability - [02-open-redirect.md](./later/02-open-redirect.md)
- JWT Security Hardening - [12-jwt-security-hardening.md](./later/12-jwt-security-hardening.md)

---

## P0 - Critical

*No outstanding P0 issues.*

---

## P1 - High

*No outstanding P1 issues.*

---

## P2 - Medium

### 1. Server-Side Validation Gap
- **Files:** `PredictionLeague.API/DependencyInjection.cs`, `PredictionLeague.Validators/`
- **Issue:** FluentValidation validators are defined for Request DTOs (e.g., `LoginRequest`), but MediatR's `ValidationBehaviour` looks for validators matching Command/Query types (e.g., `LoginCommand`). No validators exist for Commands/Queries.
- **Impact:** Server-side validation is bypassed for direct API calls. Client-side (Blazor) validation still works but can be bypassed by attackers.
- **Mitigation:** Already mitigated by:
  - Client-side FluentValidation in Blazor forms
  - Domain model guards (Ardalis.GuardClauses)
  - Database constraints
- **Plan:** [26-server-validation-gap.md](./26-server-validation-gap.md)

### 2. Entry Code Character Validation Missing
- **File:** `PredictionLeague.Validators/Leagues/JoinLeagueRequestValidator.cs` (lines 12-14)
- **Issue:** Entry codes only validate length (6 characters), not that they are alphanumeric.
- **Impact:** Allows special characters, unicode, or injection attempts in entry codes.
- **Plan:** [27-entry-code-validation.md](./27-entry-code-validation.md)

### 3. Football API Response Handling
- **Files:**
  - `PredictionLeague.Infrastructure/Services/FootballDataService.cs`
  - `PredictionLeague.Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs`
- **Issue:** API responses deserialized without explicit validation; potential null reference issues.
- **Impact:** Service disruption if API changes response structure or returns unexpected data.
- **Plan:** [28-football-api-handling.md](./28-football-api-handling.md)

---

## Low Priority

### 1. Exception Messages Reveal Implementation Details
- **File:** `PredictionLeague.API/Middleware/ErrorHandlingMiddleware.cs`
- **Issue:** Raw exception messages are returned for `KeyNotFoundException`, `ArgumentException`, `InvalidOperationException`.
- **Impact:** Information disclosure about business logic and system state.

### 2. User IDs Exposed in Public DTOs
- **Files:** Multiple DTOs in `PredictionLeague.Contracts/`
- **Issue:** Internal user IDs (GUIDs) exposed in leaderboard and prediction response DTOs.
- **Impact:** Enables user enumeration and potential targeting of specific users.

### 3. Deprecated X-XSS-Protection Header
- **File:** `PredictionLeague.API/Middleware/SecurityHeadersMiddleware.cs`
- **Issue:** `X-XSS-Protection` header included for backwards compatibility but deprecated in modern browsers.
- **Impact:** None (included for older browser support).

### 4. Remove Legacy Package References (Housekeeping)
- **Files:** `Web.Client.csproj`, `Application.csproj`
- **Issue:** Legacy `Microsoft.AspNetCore.Identity` and `Authentication.Abstractions` packages (2.3.9) remain but are provided by .NET 10 shared framework.
- **Impact:** None (at latest version, functionality duplicated by framework).
- **Plan:** [29-remove-legacy-package-references.md](./29-remove-legacy-package-references.md)

---

## Implementation Order

### Phase 1: Critical (Immediate)
- [x] Add `app.UseRateLimiter()` to middleware pipeline

### Phase 2: High Priority (This Sprint)
- [x] Add membership validation to GetLeagueDashboardRoundResultsQuery
- [x] Return generic error message for registration
- [x] Upgrade outdated NuGet packages (.NET 10, JWT 8.15.0)

### Phase 3: Medium Priority (Next Sprint)
- [ ] Align validators with Commands/Queries OR add ASP.NET Core auto-validation
- [ ] Add alphanumeric validation to entry codes
- [ ] Add Football API response validation

### Phase 4: Ongoing
Remaining ongoing activities:
1. Dependency updates (monitor for new versions)
2. Security monitoring
3. Address low priority items

---

## Positive Security Controls

The following are properly implemented:

- SQL Injection Prevention (parameterised Dapper queries)
- Rate Limiting (tiered policies: 100/min global, 10/5min auth, 60/min API)
- Security Headers (CSP, X-Frame-Options, HSTS, etc.)
- API Key Protection (constant-time comparison)
- Role-Based Authorization (admin endpoints)
- Password Hashing (ASP.NET Identity)
- Password Policy (8+ chars, uppercase, lowercase, digit, lockout after 5 attempts)
- Refresh Token Rotation
- HttpOnly Refresh Cookies
- Error Handling (stack traces hidden in production)
- Secrets Management (Azure Key Vault)
- CORS Hardening (restricted methods and headers)
- Input Validation (FluentValidation with safe character patterns - client-side)
- XSS Prevention (escapeHtml in JavaScript, Blazor auto-encoding, NameValidator sanitisation)
- Boost Race Condition Protection (database unique constraint)
- Deadline Enforcement (server-side UTC checks)

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.

**Current approach:**
1. Manual testing of attack vectors
2. Verification that existing functionality is not broken
3. Security header validation via securityheaders.com

**Future (when test projects are added):**
1. Unit tests for security logic
2. Integration tests for endpoints
3. Automated security regression tests

---

## References

- [OWASP Top 10 2021](https://owasp.org/www-project-top-ten/)
- [OWASP ASVS v4.0](https://owasp.org/www-project-application-security-verification-standard/)
- [CWE/SANS Top 25](https://cwe.mitre.org/top25/)
- [Microsoft Secure Coding Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)
