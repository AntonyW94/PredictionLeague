# Feature: Daily Production Database Backup

## Status

Not Started | **In Progress** | Complete

## Summary

Create a daily backup of the production database (`ThePredictions`) to a separate backup database (`ThePredictionsBackup`) on the same server. This is a full, unmodified copy — no anonymisation — providing a safety net independent of Fasthosts' own backup policy.

## Key Details

| Component | Value |
|-----------|-------|
| Source Database | `ThePredictions` (read via `Refresh` login) |
| Backup Database | `ThePredictionsBackup` |
| Backup DB Login | `BackupWriter` (create in Fasthosts) |
| Schedule | Daily at 2am UTC + manual trigger |
| Anonymisation | None — this is an exact copy |
| Tool | `tools/ThePredictions.DatabaseTools/` (mode: `ProductionBackup`) |
| Workflow | `.github/workflows/backup-prod-db.yml` |

## Implementation Plan

### Job 1: Manual Infrastructure Setup (Prerequisites)

These steps must be completed manually before the workflow can run.

#### 1. Create `ThePredictionsBackup` database in Fasthosts

- Log in to the Fasthosts control panel
- Create a new SQL Server database named `ThePredictionsBackup` on the same server as `ThePredictions`

#### 2. Create `BackupWriter` login

- In Fasthosts, create a new SQL login named `BackupWriter`
- Grant it read/write access to `ThePredictionsBackup` (it needs `db_datareader`, `db_datawriter`, and `db_ddladmin` to manage constraints)

#### 3. Apply schema to backup database

- Connect to `ThePredictionsBackup` using SSMS or Azure Data Studio
- Run the same schema creation scripts used for production to create all tables, including:
  - All tables in `TableCopyOrder`: `AspNetRoles`, `AspNetUsers`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `Teams`, `Seasons`, `Rounds`, `Matches`, `BoostDefinitions`, `Leagues`, `LeagueMembers`, `LeagueMemberStats`, `LeagueBoostRules`, `LeagueBoostWindows`, `LeaguePrizeSettings`, `UserPredictions`, `RoundResults`, `LeagueRoundResults`, `UserBoostUsages`, `Winnings`
  - All tables in `TablesToSkip`: `AspNetUserTokens`, `RefreshTokens`, `PasswordResetTokens`
- All foreign key constraints must match production

#### 4. Add `BACKUP_CONNECTION_STRING` GitHub secret

- Go to the GitHub repository Settings > Secrets and variables > Actions
- Add a new secret named `BACKUP_CONNECTION_STRING`
- Value: connection string using the `BackupWriter` login pointing at `ThePredictionsBackup`

#### Prerequisites checklist

- [ ] Create `ThePredictionsBackup` database in Fasthosts
- [ ] Create `BackupWriter` login in Fasthosts
- [ ] Apply schema to backup database
- [ ] Add `BACKUP_CONNECTION_STRING` GitHub secret

### Job 2: GitHub Actions Workflow

**File:** `.github/workflows/backup-prod-db.yml`

- Runs daily at 2am UTC via cron schedule
- Also supports manual triggering via `workflow_dispatch`
- Uses the existing `ProductionBackup` mode — no tool code changes needed
- Requires `PROD_CONNECTION_STRING` and `BACKUP_CONNECTION_STRING` secrets

### Job 3: Verify and Go Live

- [ ] Trigger the workflow manually via `workflow_dispatch` to verify it works
- [ ] Check the backup database contains all expected data
- [ ] Confirm the daily cron schedule runs successfully
- [ ] Mark this feature as Complete

## Notes

- This contains real personal data — treat the backup DB credentials with the same care as production
- The existing `Refresh` login on prod is reused for reading
- Uses the same `ThePredictions.DatabaseTools` project as the dev refresh, with `ProductionBackup` mode (skips anonymisation and test accounts)
- Token tables (`AspNetUserTokens`, `RefreshTokens`, `PasswordResetTokens`) are excluded — they are either always empty or contain short-lived tokens that regenerate on login
- GitHub's default email notifications will alert on workflow failures

## Related

- [`tools/ThePredictions.DatabaseTools/`](../../../../tools/ThePredictions.DatabaseTools/) — the dev database refresh tool (shares the same project)
- [`.github/workflows/backup-prod-db.yml`](../../../../.github/workflows/backup-prod-db.yml) — the backup workflow
- [`.github/workflows/refresh-dev-db.yml`](../../../../.github/workflows/refresh-dev-db.yml) — the dev refresh workflow (similar pattern)
