# Security Audit History

This document consolidates the history of security audits and fixes for PredictionLeague. It serves as a reference for future audits and documents accepted risks.

## Audit Summary

| Audit Date | Findings | Completed | Deferred | Accepted |
|------------|----------|-----------|----------|----------|
| January 24, 2026 | Initial audit | - | - | - |
| January 25, 2026 | Comprehensive follow-up | - | - | - |
| January 27, 2026 | Third audit | - | - | - |
| **Total** | **39** | **34** | **5** | **3** |

**Next scheduled review:** July 2026

---

## Completed Security Fixes

All fixes below have been implemented and verified.

### Critical (P0) - Fixed

| Fix | Description | CWE |
|-----|-------------|-----|
| TasksController `[AllowAnonymous]` bypass | Removed inappropriate anonymous access | - |
| ErrorHandlingMiddleware not registered | Registered middleware in pipeline | - |
| XSS via user names | Added NameValidator for sanitisation | CWE-79 |
| IDOR: Unauthorized League Update | Added ownership validation to UpdateLeagueCommand | CWE-639 |
| Password Hash in DTO | Removed PasswordHash from UserDto | CWE-200 |
| IDOR: League Members Access | Added membership validation | CWE-639 |
| IDOR: Leaderboard Access | Added membership validation | CWE-639 |

### High (P1) - Fixed

| Fix | Description | CWE |
|-----|-------------|-----|
| API Key Timing Attack | Implemented constant-time comparison | CWE-208 |
| Rate Limiting | Added tiered rate limiting (100/min global, 10/5min auth, 60/min API) | CWE-770 |
| Security Headers | Added CSP, X-Frame-Options, HSTS, etc. | Multiple |
| Handler Authorization | Added authorization checks to all handlers | CWE-862 |
| Missing Validators | Added validators for all commands/queries | CWE-20 |
| Boost System Race Condition | Added database unique constraint | CWE-362 |
| IDOR: League Data Endpoints | Added membership validation | CWE-639 |
| Sensitive Data Logging | Removed sensitive data from log statements | CWE-532 |
| Boost Deadline Enforcement | Added server-side UTC deadline checks | CWE-20 |
| Admin Command Validators | Added validators for admin commands | CWE-20 |
| Configuration Hardening | Secured configuration values | CWE-16 |
| JavaScript XSS Prevention | Added escapeHtml function, Blazor auto-encoding | CWE-79 |
| Rate Limiting Middleware Enabled | Called `app.UseRateLimiter()` in pipeline | CWE-770 |
| IDOR: Round Results Access | Added membership validation to GetLeagueDashboardRoundResultsQuery | CWE-639 |
| Email Enumeration via Registration | Return generic error message | CWE-204 |
| Entry Code Character Validation | Added alphanumeric validation | CWE-20 |
| Football API Response Handling | Added response validation | CWE-20 |

### Medium/Low - Fixed

| Fix | Description |
|-----|-------------|
| Login Password MaxLength | Added to LoginRequestValidator |
| League Name Character Validation | Added LeagueNameValidationExtensions |
| Access Token Expiry | Reduced from 60 to 15 minutes |
| brevo_csharp package | Verified at latest version (1.1.1) |
| Password Policy Configuration | Configured ASP.NET Identity options |
| CORS Hardening | Restricted methods and headers |
| Lock Scoring Configuration | Secured by design - values immutable after creation |
| ShortName Validation | Added to BaseTeamRequestValidator |
| Season Name Character Validation | Added SafeNameValidationExtensions |
| Outdated Packages Updated | .NET 10, JWT 8.15.0, framework packages 10.0.2 |
| Legacy Package References | Removed unused package references |

---

## Deferred Security Items

These items have been reviewed and deferred due to architectural constraints or requiring login system changes. Full plans available in `docs/todo/security/`.

| Item | Reason | Plan Location |
|------|--------|---------------|
| Open Redirect Vulnerability | Requires login system changes | `todo/security/open-redirect/` |
| JWT Security Hardening | Mobile browser cookie compatibility | `todo/security/jwt-security-hardening/` |
| Refresh Tokens in URLs | Mobile browser OAuth compatibility | `todo/security/refresh-tokens-in-urls/` |
| Access Tokens in localStorage | Blazor WASM architectural constraint | `todo/security/localstorage-tokens/` |
| Server-Side Validation Gap | FluentValidation.AspNetCore deprecated | `todo/security/server-validation-gap/` |

---

## Accepted Risks & Design Decisions

These findings have been reviewed and accepted as intentional design decisions or acceptable trade-offs.

### 1. Detailed Exception Messages Returned to Users

| Attribute | Value |
|-----------|-------|
| **File** | `PredictionLeague.API/Middleware/ErrorHandlingMiddleware.cs` |
| **Finding** | Raw exception messages returned for `KeyNotFoundException`, `ArgumentException`, `InvalidOperationException` |
| **CWE** | CWE-209 (Information Exposure Through Error Message) |
| **Decision** | **Accepted** |
| **Rationale** | Detailed error messages improve user experience by helping users understand and correct their input. Messages are logged to Datadog for support investigations. |
| **Mitigations** | - All exceptions logged with full details to Datadog<br>- Stack traces hidden in production (only shown in Development)<br>- No secrets or credentials exposed in messages |
| **Reviewed** | January 27, 2026 |

### 2. X-XSS-Protection Header Included

| Attribute | Value |
|-----------|-------|
| **File** | `PredictionLeague.API/Middleware/SecurityHeadersMiddleware.cs` |
| **Finding** | Deprecated `X-XSS-Protection: 1; mode=block` header included |
| **Decision** | **Accepted** |
| **Rationale** | Kept for backwards compatibility with older browsers. Modern browsers ignore it. |
| **Mitigations** | - Strong CSP provides better XSS protection<br>- Blazor auto-encoding prevents XSS<br>- NameValidator sanitises user input |
| **Reviewed** | January 27, 2026 |

### 3. User IDs (GUIDs) Exposed in Public DTOs

| Attribute | Value |
|-----------|-------|
| **Files** | Multiple DTOs in `PredictionLeague.Contracts/` |
| **Finding** | Internal user IDs exposed in leaderboard and prediction response DTOs |
| **CWE** | CWE-200 (Exposure of Sensitive Information) |
| **Decision** | **Accepted** |
| **Rationale** | GUIDs are non-sequential and provide no privilege escalation. Required for client-side state management in Blazor WASM. |
| **Mitigations** | - All data access enforces membership checks<br>- GUIDs cannot be enumerated or predicted<br>- No sensitive operations possible with just a user ID |
| **Reviewed** | January 27, 2026 |

---

## Architectural Constraints (Documented)

### 4. Access Tokens Stored in localStorage

| Attribute | Value |
|-----------|-------|
| **File** | `PredictionLeague.Web.Client/` (Blazor WASM) |
| **Finding** | JWT access tokens stored in browser localStorage |
| **CWE** | CWE-922 (Insecure Storage of Sensitive Information) |
| **Decision** | **Deferred** - Blazor WASM architectural constraint |
| **Rationale** | Blazor WASM runs entirely client-side; HttpOnly cookies would require a BFF (Backend for Frontend) pattern which adds significant complexity. |
| **Mitigations** | - Access token expiry reduced to 15 minutes<br>- Strong CSP prevents XSS attacks<br>- Refresh tokens use HttpOnly cookies<br>- Input validation and output encoding throughout |
| **Reviewed** | January 27, 2026 |

### 5. Refresh Tokens in URL Parameters

| Attribute | Value |
|-----------|-------|
| **File** | `PredictionLeague.API/Controllers/ExternalAuthController.cs` |
| **Finding** | Refresh token passed in URL during Google OAuth callback |
| **CWE** | CWE-598 (Information Exposure Through Query Strings) |
| **Decision** | **Deferred** - Mobile browser compatibility |
| **Rationale** | Required for mobile browser OAuth flows where cookies may not be reliably set during redirects. |
| **Mitigations** | - Tokens are single-use and short-lived<br>- HTTPS enforced (tokens encrypted in transit)<br>- Refresh token rotation enabled |
| **Reviewed** | January 27, 2026 |

### 6. SameSite=None on Refresh Token Cookies

| Attribute | Value |
|-----------|-------|
| **File** | `PredictionLeague.Infrastructure/Authentication/` |
| **Finding** | Refresh token cookies set with `SameSite=None` |
| **Decision** | **Deferred** - Required for cross-site authentication |
| **Rationale** | Necessary for the OAuth flow where the API and client may be on different subdomains. |
| **Mitigations** | - Cookies marked `Secure` (HTTPS only)<br>- Cookies marked `HttpOnly`<br>- Refresh token rotation invalidates old tokens |
| **Reviewed** | January 27, 2026 |

### 7. Server-Side Validation Gap (MediatR Pipeline)

| Attribute | Value |
|-----------|-------|
| **Files** | `PredictionLeague.Validators/` validators vs MediatR Commands/Queries |
| **Finding** | FluentValidation validators defined for Request DTOs are not automatically triggered; MediatR's `ValidationBehaviour` looks for Command/Query validators |
| **CWE** | CWE-20 (Improper Input Validation) |
| **Decision** | **Deferred** - FluentValidation.AspNetCore deprecated |
| **Rationale** | The recommended fix (`FluentValidation.AspNetCore` auto-validation) has been deprecated by the FluentValidation team. Alternative fixes require duplicating validation logic across ~50+ Command validators. |
| **Mitigations** | - Client-side validation via `FluentValidationValidator` in Blazor forms<br>- Domain model guards (`Ardalis.GuardClauses`) in entity constructors<br>- Database constraints (column lengths, types, relationships)<br>- ASP.NET model binding provides basic type validation |
| **Reviewed** | January 29, 2026 |

---

## Scanner False Positives

These items may be flagged by automated security scanners but are not vulnerabilities in this context.

### 8. Missing X-XSS-Protection Header

Some scanners may flag the absence of `X-XSS-Protection`. In this application, the header IS present for backwards compatibility. Modern security guidance recommends either omitting it or setting it to `0`, but we keep it for older browser support.

### 9. JWT Algorithm Not Restricted

Some scanners flag that JWT accepts multiple algorithms. In this application:
- Only HS256 is configured in `TokenValidationParameters`
- The `ValidAlgorithms` property could be added for defence-in-depth but is not critical

### 10. Rate Limiting Not Detected

Some scanners cannot detect rate limiting configured in code. This application has:
- Global rate limit: 100 requests/minute per IP
- Auth endpoints: 10 requests/5 minutes per IP
- API endpoints: 60 requests/minute per IP

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

## Review Process

Items should be reviewed when:
- The application's threat model changes
- New attack vectors are discovered
- Compliance requirements change
- Major architectural changes occur

---

## References

- [OWASP Top 10 2021](https://owasp.org/www-project-top-ten/)
- [OWASP ASVS v4.0](https://owasp.org/www-project-application-security-verification-standard/)
- [CWE/SANS Top 25](https://cwe.mitre.org/top25/)
- [Microsoft Secure Coding Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)
- [OWASP Risk Rating Methodology](https://owasp.org/www-community/OWASP_Risk_Rating_Methodology)
- [NIST Risk Management Framework](https://csrc.nist.gov/projects/risk-management)
