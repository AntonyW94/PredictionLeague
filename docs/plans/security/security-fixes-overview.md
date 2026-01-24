# Security Fixes Plan

## Overview

This document outlines the security vulnerabilities identified in the PredictionLeague application during the January 2026 security audit. Each issue is categorized by priority and linked to a detailed implementation plan.

## Audit Date
January 24, 2026

## Summary

| Priority | Count | Description |
|----------|-------|-------------|
| P0 - Critical | 1 | Fix immediately - active exploitation risk |
| P1 - High | 5 | Fix this sprint - significant security impact |
| P2 - Medium | 3 | Fix soon - defense in depth |
| Handler Fixes | 14 | Ongoing - authorization improvements |

## Already Fixed

- [x] TasksController `[AllowAnonymous]` bypass
- [x] ErrorHandlingMiddleware not registered
- [x] XSS via user names (NameValidator added)

## Intentionally Deferred

The following issues have been deferred due to mobile browser cookie compatibility constraints:
- Refresh tokens in URLs
- Refresh token logging
- Authentication flow changes

---

## P0 - Critical

| # | Issue | Status | Plan |
|---|-------|--------|------|
| 1 | IDOR: Unauthorized League Update | Open | [01-idor-league-update.md](./01-idor-league-update.md) |

---

## P1 - High

| # | Issue | Status | Plan |
|---|-------|--------|------|
| 2 | Open Redirect Vulnerability | Open | [02-open-redirect.md](./02-open-redirect.md) |
| 3 | API Key Timing Attack | Open | [03-api-key-timing-attack.md](./03-api-key-timing-attack.md) |
| 4 | Password Hash in DTO | Open | [04-password-hash-disclosure.md](./04-password-hash-disclosure.md) |
| 5 | IDOR: League Members Access | Open | [05-idor-league-members.md](./05-idor-league-members.md) |
| 6 | IDOR: Leaderboard Access | Open | [06-idor-leaderboards.md](./06-idor-leaderboards.md) |

---

## P2 - Medium

| # | Issue | Status | Plan |
|---|-------|--------|------|
| 7 | No Rate Limiting | Open | [07-rate-limiting.md](./07-rate-limiting.md) |
| 8 | Missing Security Headers | Open | [08-security-headers.md](./08-security-headers.md) |
| 9 | Missing Validators | Open | [09-missing-validators.md](./09-missing-validators.md) |

---

## Handler Authorization Fixes

| # | Issue | Status | Plan |
|---|-------|--------|------|
| 10 | Handler Authorization Improvements | Open | [10-handler-authorization.md](./10-handler-authorization.md) |

---

## Implementation Order

Recommended sequence for implementation:

1. **Week 1**: P0 (IDOR League Update) + P1 critical path (Open Redirect, API Key Timing)
2. **Week 2**: P1 remaining (Password Hash, IDOR Members/Leaderboards)
3. **Week 3**: P2 (Rate Limiting, Security Headers, Validators)
4. **Ongoing**: Handler authorization improvements as code is touched

---

## Testing Requirements

> **Note**: Automated testing is deferred until a test project infrastructure is in place.
> Test code examples are preserved in each plan for future implementation.

**Current approach (this implementation round):**
1. Manual testing of attack vectors
2. Verification that existing functionality is not broken

**Future (when test projects are added):**
1. Unit tests for the fix logic
2. Integration tests for the endpoint
3. Regression tests for security vulnerabilities

---

## References

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/)
- [CWE/SANS Top 25](https://cwe.mitre.org/top25/)
