# Session Timeout and Management

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped. JWT access tokens (15 min) plus DB-persisted refresh tokens (30-day sliding window) with rotation and revocation via `RefreshTokenCommand`/`Handler` and `IRefreshTokenRepository`.

## Summary

Manages user sessions with configurable timeouts. Provides warnings when sessions are about to expire due to inactivity and allows graceful re-authentication without losing context.

## Priority

**Medium** (from roadmap)

## Requirements

- [ ] Configurable session timeout
- [ ] Idle timeout warning
- [ ] Graceful re-authentication
