# Security Fixes Plan

## Overview

This document outlines the security vulnerabilities identified in the PredictionLeague application. The original audit was conducted January 24, 2026, with a comprehensive follow-up audit on January 25, 2026.

## Audit Dates
- **Initial Audit:** January 24, 2026
- **Comprehensive Follow-up:** January 25, 2026

## Summary

| Priority | Count | Description |
|----------|-------|-------------|
| Completed | 5 | Fixes implemented and verified |
| Deferred | 2 | Require login system changes |
| P0 - Critical | 1 | Fix immediately - active exploitation risk |
| P1 - High | 5 | Fix this sprint - significant security impact |
| P2 - Medium | 5 | Fix soon - defense in depth |
| Low | 4 | Minor improvements |

**Total Findings:** 22 (5 completed, 2 deferred)

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

## Intentionally Deferred

**Deferred plans moved to:** [`./later/`](./later/)

The following issues have been deferred due to mobile browser cookie compatibility constraints or require login system changes:
- Refresh tokens in URLs (ExternalAuthController)
- Access tokens in localStorage (Blazor WASM architectural decision)
- Open Redirect Vulnerability - [02-open-redirect.md](./later/02-open-redirect.md)
- JWT Security Hardening - [12-jwt-security-hardening.md](./later/12-jwt-security-hardening.md)

---

## P0 - Critical

| # | Issue | Status | Plan |
|---|-------|--------|------|
| 11 | Boost System Race Condition (Double Boost) | Open | [11-boost-race-condition.md](./11-boost-race-condition.md) |

---

## P1 - High

| # | Issue | Status | Plan |
|---|-------|--------|------|
| 4 | Password Hash in DTO | Open | [04-password-hash-disclosure.md](./04-password-hash-disclosure.md) |
| 5 | IDOR: League Members Access | Open | [05-idor-league-members.md](./05-idor-league-members.md) |
| 6 | IDOR: Leaderboard Access | Open | [06-idor-leaderboards.md](./06-idor-leaderboards.md) |
| 13 | IDOR: League Data (5 endpoints) | Open | [13-idor-league-data-endpoints.md](./13-idor-league-data-endpoints.md) |
| 14 | Sensitive Data Logging (Tokens/Emails) | Open | [14-sensitive-data-logging.md](./14-sensitive-data-logging.md) |

---

## P2 - Medium

| # | Issue | Status | Plan |
|---|-------|--------|------|
| 9 | Missing Validators | Partial | [09-missing-validators.md](./09-missing-validators.md) |
| 17 | Boost Deadline Enforcement | Open | [17-boost-deadline-enforcement.md](./17-boost-deadline-enforcement.md) |
| 19 | Admin Command Validators | Open | [19-admin-command-validators.md](./19-admin-command-validators.md) |
| 20 | Configuration Hardening (HSTS, AllowedHosts) | Open | [20-configuration-hardening.md](./20-configuration-hardening.md) |
| 21 | JavaScript XSS Prevention | Open | [21-javascript-xss-prevention.md](./21-javascript-xss-prevention.md) |

---

## Low Priority

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| - | Vulnerable NuGet Package (brevo_csharp) | Open | Monitor for updates |
| - | Login Password MaxLength | Open | Add to validators |
| - | League Name Character Validation | Open | Add to validators |
| - | Access Token Expiry (60 min) | Open | Consider reducing |

---

## Comprehensive Audit Report

For full details on all findings, see:
**[security-audit-report-2026-01-25.md](./security-audit-report-2026-01-25.md)**

---

## Implementation Order

### Phase 1: Critical (Immediate)
1. **Boost Race Condition** - Add database UNIQUE constraint

### Phase 2: High Priority (This Sprint)
2. **Password Hash in DTO** - Remove from ApplicationUserDto
3. **IDOR Fixes** - League members, leaderboards, league data endpoints
4. **Sensitive Data Logging** - Remove token/email logging

### Phase 3: Medium Priority (Next Sprint)
5. **Missing Validators** - Complete remaining validators
6. **Boost Deadline Enforcement**
7. **Admin Command Validators**
8. **Configuration Hardening** (HSTS, AllowedHosts)
9. **JavaScript XSS Prevention**

### Phase 4: Ongoing
10. Low priority items
11. Dependency updates
12. Security monitoring

---

## Positive Security Controls

The following are properly implemented:

- SQL Injection Prevention (parameterised Dapper queries)
- Rate Limiting (tiered policies)
- Security Headers (CSP, X-Frame-Options, etc.)
- API Key Protection (constant-time comparison)
- Role-Based Authorization (admin endpoints)
- Password Hashing (ASP.NET Identity)
- Refresh Token Rotation
- HttpOnly Refresh Cookies
- Error Handling (stack traces hidden in production)
- Secrets Management (Azure Key Vault)

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
