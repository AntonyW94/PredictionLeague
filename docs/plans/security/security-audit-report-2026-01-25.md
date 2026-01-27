# Comprehensive Security Audit Report

**Application:** PredictionLeague
**Audit Date:** January 25, 2026
**Auditor:** Claude Security Audit
**Scope:** Full application security review (follow-up audit)

---

## Executive Summary

This comprehensive security audit identified **28 security issues** across the PredictionLeague application. The audit covered authentication, authorization, input validation, data protection, API security, infrastructure, business logic, and Blazor WASM-specific concerns.

**Key Risk Areas:**
- Business logic vulnerabilities in the boost system (race conditions)
- Token security issues (localStorage storage, SameSite settings)
- Multiple IDOR vulnerabilities in league data endpoints
- Sensitive data exposure in logs
- Missing server-side validation for some client operations

---

## Findings Summary

| Severity | Count | Key Issues |
|----------|-------|------------|
| **Critical** | 4 | Boost race condition, JWT SameSite, Token in URL, Access tokens in localStorage |
| **High** | 9 | IDOR (5 endpoints), Token logging, Email logging, Password policy, CORS config |
| **Medium** | 11 | Boost deadline bypass, Score point changes, Missing validators, AllowedHosts, HSTS, XSS |
| **Low** | 4 | Various validation gaps |

---

## Critical Findings (P0)

### 1. Boost System Race Condition - Double Boost Exploit
**Severity:** CRITICAL
**Status:** NEW
**File:** `PredictionLeague.Infrastructure/Repositories/Boosts/BoostWriteRepository.cs`

**Issue:** No unique constraint on UserBoostUsages table allows concurrent requests to apply the same boost multiple times.

**Impact:** Users can achieve 4x points instead of 2x by sending simultaneous boost requests.

**Fix Plan:** [11-boost-race-condition.md](./11-boost-race-condition.md)

---

### 2. SameSite=None on Refresh Token Cookies
**Severity:** CRITICAL
**Status:** NEW
**File:** `PredictionLeague.API/Controllers/AuthControllerBase.cs:24`

**Issue:** Refresh token cookies set with `SameSite = SameSiteMode.None` allows cross-site cookie transmission.

**Impact:** Enables CSRF attacks on token refresh endpoint.

**Fix Plan:** [12-jwt-security-hardening.md](./12-jwt-security-hardening.md)

---

### 3. Refresh Token Exposed in URL Query Parameter
**Severity:** CRITICAL
**Status:** KNOWN (Deferred for mobile compatibility)
**File:** `PredictionLeague.API/Controllers/ExternalAuthController.cs:81`

**Issue:** After Google OAuth callback, refresh tokens passed via URL query parameter.

**Impact:** Tokens logged in server access logs, browser history, and proxy logs.

**Status:** Documented as deferred due to mobile browser cookie constraints.

---

### 4. Access Tokens Stored in localStorage
**Severity:** CRITICAL
**Status:** KNOWN (Documented trade-off)
**File:** `PredictionLeague.Web.Client/Authentication/ApiAuthenticationStateProvider.cs`

**Issue:** JWT access tokens stored in browser localStorage, vulnerable to XSS.

**Impact:** Any XSS vulnerability exposes all user tokens for 60-minute window.

**Mitigation:** Strong CSP policy implemented. Consider moving to HttpOnly cookies.

---

## High Findings (P1)

### 5-9. IDOR Vulnerabilities (5 Endpoints)
**Severity:** HIGH
**Status:** NEW
**Files:** `PredictionLeague.API/Controllers/LeaguesController.cs`

| Endpoint | Issue |
|----------|-------|
| `GET /api/leagues/{id}` | Entry code exposed to non-members |
| `GET /api/leagues/{id}/prizes` | Prize structure visible to non-members |
| `GET /api/leagues/{id}/winnings` | Winner details visible to non-members |
| `GET /api/leagues/{id}/rounds-for-dashboard` | Round data visible to non-members |
| `GET /api/leagues/{id}/months` | Monthly data visible to non-members |

**Fix Plan:** [13-idor-league-data-endpoints.md](./13-idor-league-data-endpoints.md)

---

### 10. Refresh Tokens Logged in Application Logs
**Severity:** HIGH
**Status:** NEW
**Files:**
- `ApiAuthenticationStateProvider.cs:95`
- `RefreshTokenCommandHandler.cs:39,44`

**Issue:** Full refresh tokens logged to Datadog/server logs.

**Fix Plan:** [14-sensitive-data-logging.md](./14-sensitive-data-logging.md)

---

### 11. Email Addresses Logged in Production
**Severity:** HIGH
**Status:** NEW
**Files:**
- `RefreshTokenCommandHandler.cs:55,61`
- `SendScheduledRemindersCommandHandler.cs:80`

**Issue:** User email addresses logged, creating PII compliance risk.

**Fix Plan:** [14-sensitive-data-logging.md](./14-sensitive-data-logging.md)

---

### 12. Insufficient Password Policy Configuration
**Severity:** HIGH
**Status:** NEW
**File:** `PredictionLeague.Infrastructure/DependencyInjection.cs`

**Issue:** ASP.NET Identity configured without explicit password requirements or account lockout.

**Fix Plan:** [15-password-policy.md](./15-password-policy.md)

---

### 13. CORS AllowAnyMethod + AllowAnyHeader Combination
**Severity:** HIGH
**Status:** NEW
**File:** `PredictionLeague.Web/Program.cs:38-48`

**Issue:** Permissive CORS policy allows any HTTP method and header from configured origin.

**Fix Plan:** [16-cors-hardening.md](./16-cors-hardening.md)

---

## Medium Findings (P2)

### 14. Boost Application After Round Deadline
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.Infrastructure/Services/BoostService.cs:81-131`

**Issue:** No deadline check when applying boosts - users can boost after round deadline.

**Fix Plan:** [17-boost-deadline-enforcement.md](./17-boost-deadline-enforcement.md)

---

### 15. League Point Values Can Be Changed Retroactively
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.Application/Features/Leagues/Commands/UpdateLeagueCommandHandler.cs`

**Issue:** League admins can change PointsForExactScore/PointsForCorrectResult after predictions submitted.

**Fix Plan:** [18-lock-scoring-configuration.md](./18-lock-scoring-configuration.md)

---

### 16. Missing Validator for UpdateMatchResultsCommand
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.Application/Features/Admin/Rounds/Commands/UpdateMatchResultsCommand.cs`

**Issue:** No validation of score ranges (0-9) for match results.

**Fix Plan:** [19-admin-command-validators.md](./19-admin-command-validators.md)

---

### 17. Missing Validator for UpdateUserRoleCommand
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.API/Controllers/Admin/UsersController.cs:48`

**Issue:** Role parameter accepts arbitrary string without enum validation.

**Fix Plan:** [19-admin-command-validators.md](./19-admin-command-validators.md)

---

### 18. AllowedHosts Wildcard Configuration
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.Web/appsettings.json`

**Issue:** `"AllowedHosts": "*"` allows requests from any Host header.

**Fix Plan:** [20-configuration-hardening.md](./20-configuration-hardening.md)

---

### 19. HSTS Header Missing from API
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.API/Middleware/SecurityHeadersMiddleware.cs`

**Issue:** API endpoints don't return HSTS header (only Web project uses UseHsts).

**Fix Plan:** [20-configuration-hardening.md](./20-configuration-hardening.md)

---

### 20. XSS in JavaScript Interop
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.Web.Client/wwwroot/js/interop.js:48-88`

**Issue:** User names interpolated into HTML/template literals without sanitization.

**Fix Plan:** [21-javascript-xss-prevention.md](./21-javascript-xss-prevention.md)

---

### 21. No JWT ClockSkew Configured
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.API/DependencyInjection.cs:46-55`

**Issue:** Default 5-minute clock skew may be inappropriate; no explicit configuration.

**Fix Plan:** [12-jwt-security-hardening.md](./12-jwt-security-hardening.md)

---

### 22. No JWT ValidateAlgorithm Whitelist
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.API/DependencyInjection.cs:46-55`

**Issue:** No explicit algorithm whitelist could allow algorithm confusion attacks.

**Fix Plan:** [12-jwt-security-hardening.md](./12-jwt-security-hardening.md)

---

### 23. ShortName Field Missing Validation
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.Validators/Admin/Teams/BaseTeamRequestValidator.cs`

**Issue:** Team ShortName not validated for length or characters.

**Fix Plan:** [22-validation-gaps.md](./22-validation-gaps.md)

---

### 24. Vulnerable NuGet Package (brevo_csharp)
**Severity:** MEDIUM
**Status:** NEW
**File:** `PredictionLeague.Infrastructure/PredictionLeague.Infrastructure.csproj`

**Issue:** brevo_csharp 1.1.1 flagged as vulnerable with explicit suppression comment.

**Fix Plan:** [23-dependency-security.md](./23-dependency-security.md)

---

## Low Findings

### 25. Login Password MaxLength Missing
**File:** `PredictionLeague.Validators/Authentication/LoginRequestValidator.cs`
**Issue:** No maximum length on password field (DoS potential)

### 26. League Name Character Validation
**File:** `PredictionLeague.Validators/Leagues/CreateLeagueRequestValidator.cs`
**Issue:** League names not validated for safe characters (HTML allowed)

### 27. Season Name Character Validation
**File:** `PredictionLeague.Validators/Admin/Seasons/BaseSeasonRequestValidator.cs`
**Issue:** Season names not validated for safe characters

### 28. Access Token Expiry (60 Minutes) May Be Long
**File:** `appsettings.json`
**Issue:** With localStorage storage, 60-minute window may be excessive

---

## Positive Security Controls Identified

The following security measures are properly implemented:

1. **SQL Injection Prevention** - All queries use parameterized Dapper commands
2. **Rate Limiting** - Global (100/min), Auth (10/5min), API (60/min) policies
3. **Security Headers** - CSP, X-Frame-Options, X-Content-Type-Options configured
4. **API Key Protection** - Constant-time comparison prevents timing attacks
5. **Role-Based Authorization** - Admin endpoints properly protected
6. **Password Hashing** - ASP.NET Identity bcrypt-based hashing
7. **Refresh Token Rotation** - Old tokens revoked on refresh
8. **HttpOnly Refresh Cookies** - Refresh tokens protected from JavaScript
9. **Error Handling** - Stack traces hidden in production
10. **Secrets Management** - Azure Key Vault with placeholder syntax

---

## Implementation Priority

### Immediate (This Sprint)
1. Boost race condition (database constraint)
2. Sensitive data logging removal
3. IDOR fixes for 5 league endpoints

### Short Term (Next Sprint)
4. JWT security hardening (SameSite, ClockSkew, Algorithm)
5. Password policy configuration
6. Boost deadline enforcement
7. Admin command validators

### Medium Term
8. CORS policy restriction
9. Configuration hardening (AllowedHosts, HSTS)
10. JavaScript XSS prevention
11. Validation gaps

### Deferred (Documented)
- Token in URL (mobile compatibility)
- localStorage tokens (architectural decision)

---

## Testing Recommendations

For each fix:
1. Manual verification of attack vector mitigation
2. Regression testing of affected functionality
3. Security scan validation

Future (when test infrastructure available):
- Unit tests for security logic
- Integration tests for endpoints
- Automated security regression tests

---

## References

- [OWASP Top 10 2021](https://owasp.org/www-project-top-ten/)
- [OWASP ASVS v4.0](https://owasp.org/www-project-application-security-verification-standard/)
- [CWE/SANS Top 25](https://cwe.mitre.org/top25/)
- [Microsoft Secure Coding Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)
