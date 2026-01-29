# Accepted Security Risks & Design Decisions

This document records security findings that have been reviewed and accepted as intentional design decisions or acceptable trade-offs. Future security audits should reference this document to avoid re-investigating known items.

**Last Updated:** January 29, 2026

---

## How to Use This Document

When conducting a security audit:
1. Check findings against this list before reporting
2. If a finding matches an entry here, it has already been evaluated
3. New findings should be added to `security-fixes-overview.md` for triage
4. Items may be revisited if the threat landscape or requirements change

---

## Intentional Design Decisions

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

## Architectural Constraints

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

## Review Process

Items should be reviewed when:
- The application's threat model changes
- New attack vectors are discovered
- Compliance requirements change
- Major architectural changes occur

**Next scheduled review:** July 2026

---

## References

- [OWASP Risk Rating Methodology](https://owasp.org/www-community/OWASP_Risk_Rating_Methodology)
- [NIST Risk Management Framework](https://csrc.nist.gov/projects/risk-management)
- Related: [`security-fixes-overview.md`](./security-fixes-overview.md)
