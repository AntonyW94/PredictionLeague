# Security Audit History

Historical record of security audits and fixes for PredictionLeague. For current accepted risks and deferred items, see [`accepted-risks.md`](accepted-risks.md).

## Audit Summary

| Audit Date | Findings | Completed | Deferred | Accepted |
|------------|----------|-----------|----------|----------|
| January 24, 2026 | Initial audit | - | - | - |
| January 25, 2026 | Comprehensive follow-up | - | - | - |
| January 27, 2026 | Third audit | - | - | - |
| **Total** | **39** | **34** | **5** | **3** |

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
| Outdated Packages Updated | JWT 8.15.0, MediatR 14.0.0, FluentValidation 12.1.1 |
| Legacy Package References | Removed unused package references |

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

## References

- [OWASP Top 10 2021](https://owasp.org/www-project-top-ten/)
- [OWASP ASVS v4.0](https://owasp.org/www-project-application-security-verification-standard/)
- [CWE/SANS Top 25](https://cwe.mitre.org/top25/)
- [Microsoft Secure Coding Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)
- [OWASP Risk Rating Methodology](https://owasp.org/www-community/OWASP_Risk_Rating_Methodology)
- [NIST Risk Management Framework](https://csrc.nist.gov/projects/risk-management)
