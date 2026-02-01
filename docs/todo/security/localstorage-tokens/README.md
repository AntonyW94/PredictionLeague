# Access Tokens Stored in localStorage

## Status

**Deferred** - Accepted architectural constraint

## Summary

JWT access tokens are stored in browser localStorage, making them accessible to any JavaScript running on the page. This is an architectural constraint of Blazor WebAssembly.

## Priority

**Deferred**

## Severity

**Medium** - Insecure Storage

## CWE Reference

CWE-922 (Insecure Storage of Sensitive Information)

## OWASP Reference

A04:2021 - Insecure Design

## Problem Description

**Affected Files:**
- `PredictionLeague.Web.Client/Services/AuthStateProvider.cs`
- `PredictionLeague.Web.Client/Services/TokenStorageService.cs`

**Security Concerns:**
- XSS attacks could steal access tokens
- Tokens persist until explicitly removed (survive browser restarts)
- No browser-enforced expiry on localStorage

## Why It's Deferred

Blazor WebAssembly runs entirely in the browser. Unlike server-side applications, it cannot:
- Use HTTP-only cookies for API authentication (CORS restrictions)
- Store tokens in server-side session state
- Access secure, non-JavaScript-accessible storage

The alternative (Backend for Frontend pattern) would require:
- A server-side component to proxy all API calls
- Session management on the server
- Significant architectural changes

## Current Mitigations

1. **Strong Content Security Policy (CSP)** - Prevents inline scripts, restricts script sources
2. **Short access token expiry** - 15 minutes limits theft window
3. **Refresh token in HTTP-only cookie** - Long-lived token is protected
4. **Input validation** - FluentValidation on all inputs
5. **Output encoding** - Blazor auto-encodes all rendered content
6. **XSS prevention** - `escapeHtml()` in JavaScript interop, NameValidator sanitises user-generated content

## Potential Future Fix

### Backend for Frontend (BFF) Pattern

```
Current:
  Blazor WASM ──(JWT in header)──> API

BFF Pattern:
  Blazor WASM ──(cookie)──> BFF Server ──(JWT)──> API
```

**Trade-offs:**
- Adds complexity and latency
- Requires server infrastructure for session state
- Loses some benefits of serverless/static hosting

## Risk Assessment

| Factor | Assessment |
|--------|------------|
| XSS likelihood | Low - Strong CSP, Blazor encoding, input validation |
| Token theft impact | Medium - 15-min expiry, refresh rotation |
| Alternative cost | High - BFF requires architectural changes |

## Decision

**Status:** Accepted architectural constraint

**Review trigger:**
- If XSS vulnerability is discovered
- If Blazor adds native secure storage support
- If application moves to server-side rendering

**Last reviewed:** January 2026
