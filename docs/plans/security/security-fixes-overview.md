# Security Fixes Plan

## Overview

This document outlines the security vulnerabilities identified in the PredictionLeague application. The original audit was conducted January 24, 2026, with a comprehensive follow-up audit on January 25, 2026.

## Audit Dates
- **Initial Audit:** January 24, 2026
- **Comprehensive Follow-up:** January 25, 2026

## Summary

| Priority | Count | Description |
|----------|-------|-------------|
| Completed | 24 | Fixes implemented and verified |
| Deferred | 4 | Require login system changes or architectural decisions |
| P0 - Critical | 0 | Fix immediately - active exploitation risk |
| P1 - High | 0 | Fix this sprint - significant security impact |
| P2 - Medium | 0 | Fix soon - defense in depth |
| Low | 0 | Minor improvements |

**Total Findings:** 28 (24 completed, 4 deferred)

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

*No remaining P0 issues.*

---

## P1 - High

*No remaining P1 issues.*

---

## P2 - Medium

*No remaining P2 issues.*

---

## Low Priority

*No remaining low priority issues.*

---

## Implementation Order

### Phase 1: Critical (Immediate)
*Completed*

### Phase 2: High Priority (This Sprint)
*Completed*

### Phase 3: Medium Priority (Next Sprint)
*Completed*

### Phase 4: Ongoing
*Low priority items completed*

Remaining ongoing activities:
1. Dependency updates (monitor for new versions)
2. Security monitoring

---

## Positive Security Controls

The following are properly implemented:

- SQL Injection Prevention (parameterised Dapper queries)
- Rate Limiting (tiered policies)
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
- Input Validation (FluentValidation with safe character patterns)

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
