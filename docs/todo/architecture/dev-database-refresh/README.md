# Feature: Development Database Refresh

## Status

**Not Started** | In Progress | Complete

## Summary

Copy anonymised production data to a development database on demand. This gives us a realistic dev environment for testing while protecting user personal data, and creates fixed test accounts for manual and E2E testing.

## User Story

As a developer, I want a dev database populated with anonymised production data so that I can test against realistic data without exposing real user information.

## Design / Mockup

```
┌─────────────────────┐         ┌─────────────────────────┐         ┌─────────────────────┐
│   Production DB     │         │  DatabaseTools            │         │   Development DB    │
│   (ThePredictions)  │────────>│  (C# Console App)        │────────>│ (ThePredictionsDev) │
│                     │  READ   │                          │  WRITE  │                     │
│  Real user data     │         │  1. Read prod tables     │         │  Anonymised data    │
│                     │         │  2. Anonymise data       │         │  + Test accounts    │
└─────────────────────┘         │  3. Truncate dev tables  │         └─────────────────────┘
                                │  4. Insert anonymised    │
                                │  5. Add test accounts    │
                                │  6. Verify personal data │
                                └─────────────────────────┘

Trigger: GitHub Actions (manual dispatch only)
```

## Database Details

| Component | Value |
|-----------|-------|
| Production Server | `mssql04.mssql.prositehosting.net` |
| Production Database | `ThePredictions` |
| Production User (refresh) | `Refresh` |
| Production Password (refresh) | `YSBe3ABkptBx2t` |
| Development Server | `mssql04.mssql.prositehosting.net` |
| Development Database | `ThePredictionsDev` |
| Development User (refresh) | `RefreshDev` |
| Development Password (refresh) | `THev_05c4r` |
| Test Account Password | `THns_05c4r` |

### Connection Strings

**Production (refresh):**
```
Server=mssql04.mssql.prositehosting.net;Database=ThePredictions;User ID=Refresh;Password=YSBe3ABkptBx2t;TrustServerCertificate=True;
```

**Development (refresh):**
```
Server=mssql04.mssql.prositehosting.net;Database=ThePredictionsDev;User ID=RefreshDev;Password=THev_05c4r;TrustServerCertificate=True;
```

## Acceptance Criteria

- [ ] Dev database has schema applied matching production
- [ ] Refresh tool copies all tables in correct dependency order
- [ ] All personal data is anonymised (emails, names, league names, entry codes)
- [ ] Passwords are invalidated and refresh tokens deleted
- [ ] Test accounts (`testplayer@dev.local`, `testadmin@dev.local`) are created with known password
- [ ] Test accounts are added to the first league automatically
- [ ] GitHub Actions workflow supports manual trigger
- [ ] No real personal data remains in dev database after refresh
- [ ] GitHub secrets are configured (not hardcoded in code)

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Apply schema to dev database](./01-apply-schema.md) | Run schema creation scripts against ThePredictionsDev | Not Started |
| 2 | [Configure GitHub secrets](./02-github-secrets.md) | Add connection strings and test password to GitHub repository secrets | Not Started |
| 3 | [Create database refresh tool](./03-create-refresh-tool.md) | Build the C# console app that copies and anonymises data | Not Started |
| 4 | [Create GitHub Actions workflow](./04-github-actions-workflow.md) | Set up the manual workflow to run the refresh tool | Not Started |
| 5 | [Run initial refresh and verify](./05-initial-refresh.md) | Manually trigger first refresh and verify everything works | Not Started |

## Dependencies

- [x] Dev database created in Fasthosts (`ThePredictionsDev`)
- [ ] Schema applied to dev database (Task 1)
- [ ] GitHub secrets configured (Task 2)

## Technical Notes

### Tables to Copy (Dependency Order)

The refresh tool must copy tables in this order to respect foreign key constraints:

| Order | Table | Contains Personal Data | Anonymisation |
|-------|-------|-------------|---------------|
| 1 | `AspNetRoles` | No | None |
| 2 | `AspNetUsers` | **Yes** | Email, FirstName, LastName, PasswordHash, PhoneNumber |
| 3 | `AspNetUserRoles` | No | None |
| 4 | `AspNetUserClaims` | No | None |
| 5 | `AspNetRoleClaims` | No | None |
| 6 | `AspNetUserLogins` | No | Delete all rows **except** `antony.willson@hotmail.com` |
| 7 | `AspNetUserTokens` | No | Delete all rows |
| 8 | `RefreshTokens` | **Yes** | Delete all rows |
| 9 | `PasswordResetTokens` | **Yes** | Delete all rows |
| 10 | `Teams` | No | None |
| 11 | `Seasons` | No | None |
| 12 | `Rounds` | No | None |
| 13 | `Matches` | No | None |
| 14 | `BoostDefinitions` | No | None |
| 15 | `Leagues` | **Yes** | Name, EntryCode |
| 16 | `LeagueMembers` | No | None |
| 17 | `LeagueMemberStats` | No | None |
| 18 | `LeagueBoostRules` | No | None |
| 19 | `LeagueBoostWindows` | No | None |
| 20 | `LeaguePrizeSettings` | No | None |
| 21 | `UserPredictions` | No | None |
| 22 | `RoundResults` | No | None |
| 23 | `LeagueRoundResults` | No | None |
| 24 | `UserBoostUsages` | No | None |
| 25 | `Winnings` | No | None |

### Personal Data Anonymisation Rules

| Field | Table | Original | Anonymised |
|-------|-------|----------|------------|
| Email | `AspNetUsers` | john.smith@gmail.com | user{N}@testmail.com |
| NormalizedEmail | `AspNetUsers` | JOHN.SMITH@GMAIL.COM | USER{N}@TESTMAIL.COM |
| UserName | `AspNetUsers` | john.smith@gmail.com | user{N}@testmail.com |
| NormalizedUserName | `AspNetUsers` | JOHN.SMITH@GMAIL.COM | USER{N}@TESTMAIL.COM |
| FirstName | `AspNetUsers` | John | TestUser{N} |
| LastName | `AspNetUsers` | Smith | Player |
| PasswordHash | `AspNetUsers` | (hashed) | INVALIDATED |
| SecurityStamp | `AspNetUsers` | (GUID) | New GUID |
| PhoneNumber | `AspNetUsers` | 07123456789 | null |
| Name | `Leagues` | Smith Family League | League {N} |
| EntryCode | `Leagues` | ABC123 | Regenerated random 6-char code |

### Preserved Account

The account `antony.willson@hotmail.com` is **excluded from anonymisation** to allow testing the Google login flow in the dev environment.

| What | Behaviour |
|------|-----------|
| `AspNetUsers` row | Copied as-is (no anonymisation) |
| `AspNetUserLogins` row(s) | Preserved for this account only (Google login) |
| All other `AspNetUserLogins` rows | Deleted as normal |

### Test Accounts

| Account | Email | Role | Password |
|---------|-------|------|----------|
| Test Player | `testplayer@dev.local` | User | `THns_05c4r` (from `TEST_ACCOUNT_PASSWORD` secret) |
| Test Admin | `testadmin@dev.local` | Admin | `THns_05c4r` (from `TEST_ACCOUNT_PASSWORD` secret) |

Both accounts are:
- Added to the first league automatically (status: Approved)
- Available immediately after DB refresh
- Usable for manual testing and future Playwright E2E tests

### Tech Stack

| Component | Technology |
|-----------|-----------|
| Refresh tool | C# console app (.NET 8) |
| Data access | Dapper + Microsoft.Data.SqlClient |
| Fake data | Bogus library |
| Password hashing | Microsoft.AspNetCore.Identity |
| CI/CD | GitHub Actions |
| Trigger | Manual dispatch only |

## Resolved Questions

- **Logging:** Minimal — only log errors and a final success/failure message. No anonymisation stats.
- **Failure notifications:** Use GitHub's built-in workflow failure notifications (watch the repo with "Workflows" checked).
- **Backup before refresh:** No — dev data is disposable and gets overwritten weekly.
