# Refresh Tokens in URL Parameters

## Status

**Deferred** - Accepted risk for mobile compatibility

## Summary

During Google OAuth callback, the refresh token is passed in the URL query string to the Blazor client. This is required for mobile browser compatibility.

## Priority

**Deferred**

## Severity

**Medium** - Token Exposure

## CWE Reference

CWE-598 (Information Exposure Through Query Strings)

## OWASP Reference

A04:2021 - Insecure Design

## Problem Description

**Current Flow:**
1. User clicks "Login with Google"
2. Redirected to Google OAuth
3. Google redirects back to API callback
4. API generates tokens
5. API redirects to Blazor with tokens in URL: `/auth/callback?accessToken=xxx&refreshToken=yyy`
6. Blazor reads tokens from URL and stores them

**Security Concerns:**
- Tokens appear in browser history
- Tokens may be logged by proxies/firewalls
- Tokens visible in referrer headers if user navigates away

## Why It's Deferred

Mobile browsers (especially Safari on iOS) have restrictions on:
- Setting cookies during cross-origin redirects
- Accessing cookies set by the API domain from the client domain

The current URL-based approach ensures reliable token delivery across all browsers and devices.

## Current Mitigations

1. **HTTPS enforced** - Tokens encrypted in transit
2. **Short-lived access tokens** - 15 minute expiry limits exposure window
3. **Refresh token rotation** - Each use generates new token, old one invalidated
4. **Single-use tokens** - Tokens consumed immediately on callback page
5. **History replacement** - Blazor replaces URL to remove tokens from history

## Potential Future Fixes

### Option A: Use HTTP-Only Cookies (Preferred)

Would require same domain for API and client, testing across all target browsers.

### Option B: Use POST with Form Auto-Submit

Return HTML page that auto-submits form to client.

### Option C: Encrypted/Signed Token Parameter

Use a short-lived, encrypted wrapper token that the client exchanges for real tokens via API call.

## Testing Required Before Any Fix

1. Test on iOS Safari (strictest cookie handling)
2. Test on Android Chrome
3. Test on desktop browsers with various privacy settings
4. Test with ad blockers enabled
5. Test in private/incognito mode

## Decision

**Status:** Accepted risk for mobile compatibility

**Review trigger:** If mobile browser cookie handling improves, or if we move to a native mobile app with different auth flows.

**Last reviewed:** January 2026
