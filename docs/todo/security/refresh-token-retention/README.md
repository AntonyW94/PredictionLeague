# Refresh Token Retention

## Status

**Not Started** | In Progress | Complete

## Summary

Expired and revoked refresh tokens are never deleted. `RefreshTokens` is the largest table in the
production database and 99.4% of it is dead rows going back a year. The scheduled cleanup job already
purges password reset tokens on exactly this pattern, so the fix is a near-copy of code that exists.

## Priority

**Medium.** Not urgent on disk - the whole table is about 2.5 MB - but it grows without bound against a
hard 1,000 MB database ceiling, and retaining a year of expired credentials works against data
minimisation. It is also the cheapest security-hygiene fix available in the codebase.

## Measured on production, 2026-08-10

| Metric | Value |
|---|---|
| Total rows | 8,714 |
| Expired (`Expires < now`) | 8,328 |
| Revoked (`Revoked IS NOT NULL`) | 6,763 |
| **Still usable** | **50** |
| Oldest `Created` | 2025-08-18 |
| Distinct users represented | 42 |
| Table size | ~2.5 MB, the largest table in the database |
| Registered users | 126 |

So 8,664 of 8,714 rows serve no purpose. With 42 users represented that is roughly 200 dead rows per
user who has ever signed in.

## Why it grows

The access token expires after 15 minutes and `RefreshTokenCommandHandler` issues a **new** refresh
token on every refresh via `IAuthenticationTokenService.GenerateTokensAsync`. The superseded row is
left in place. An active session therefore adds a row every 15 minutes, indefinitely.

Nothing deletes them:

- `src/ThePredictions.Infrastructure/Repositories/RefreshTokenRepository.cs` exposes `CreateAsync`,
  `GetByTokenAsync`, `RevokeAllForUserAsync` and `UpdateAsync`. **There is no delete.**
- `CleanupExpiredDataCommandHandler` purges password reset tokens only, and returns
  `CleanupResult(int PasswordResetTokensDeleted)`.

`RevokeAllForUserAsync` sets a `Revoked` timestamp rather than deleting, which is correct for
invalidation but means logout adds to the dead rows rather than removing them.

## Requirements

- [ ] `DeleteExpiredAsync(DateTime cutoffUtc, CancellationToken)` on `IRefreshTokenRepository` and
      `RefreshTokenRepository`
- [ ] Call it from `CleanupExpiredDataCommandHandler`
- [ ] Extend `CleanupResult` with the count, and update `CleanupExpiredDataCommandHandlerTests`
- [ ] Batch the delete so the first run against the backlog does not hold a long transaction
- [ ] Inject `IDateTimeProvider` into the handler while there (see below)
- [ ] Verify the count reported by the scheduled job in the logs after the first production run

## Design detail

### Keep a retention window, do not delete on expiry

Delete rows where the token expired **or** was revoked more than 30 days ago, rather than the moment it
becomes unusable. That leaves a short trail of recent sessions for investigating a support question or
a suspicious login, and it matches the 30-day constant the handler already uses for password reset
tokens.

```sql
DELETE TOP (@BatchSize) FROM [RefreshTokens]
WHERE ([Expires] < @CutoffUtc)
   OR ([Revoked] IS NOT NULL AND [Revoked] < @CutoffUtc);
```

### Batch it

The first production run has roughly 8,600 rows to remove. That is small, but the same job will run
against much larger backlogs if it is ever disabled for a while, and the shared-hosting database has a
100 MB log cap. Loop `DELETE TOP (1000)` until zero rows are affected rather than issuing one
unbounded delete.

### Take the clock as a dependency

`CleanupExpiredDataCommandHandler` currently reads `DateTime.UtcNow` directly and is one of the six
entries on the burn-down allowlist in
`tests/Unit/ThePredictions.Conventions.Tests.Unit/ClockAccessConventionTests.cs`. Injecting
`IDateTimeProvider` as part of this change makes the cutoff testable and lets that entry be removed -
and the conventions test will **require** the entry to be removed, because it fails on a stale
allowlist as well as on a new violation.

## Not a schema change

No migration is needed. This deletes rows from an existing table using existing columns, so
`docs/guides/database-schema.md` and the `DatabaseTools` copy order are unaffected.
`RefreshTokens` is already excluded from both the dev refresh and the production backup as a token
table, so nothing downstream sees the change either.

## Worth doing at the same time

The client fires **three** `POST /api/authentication/refresh-token` requests per anonymous page load,
all returning 400, visible in the browser console on every visit. They are harmless - an anonymous
visitor has no refresh cookie, so 400 is the correct answer - but three attempts where one would do
suggests `ApiAuthenticationStateProvider` is racing itself during start-up. Worth a look while in this
area, though it is a separate fix.

## Notes

- Verified read-only against production on 2026-08-10. An earlier figure of 26,142 rows quoted during
  that investigation was wrong: it came from a query joining `sys.allocation_units`, which multiplies
  the row count by the number of allocation units per table. The correct count is 8,714.
- The 1,000 MB database cap is confirmed on both dev and production. See
  [`docs/todo/architecture/image-storage/`](../../architecture/image-storage/) for the headroom
  analysis, which shares the same ceiling.
