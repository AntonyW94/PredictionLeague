# Feature: Daily Production Database Backup

## Status

**Not Started** | In Progress | Complete

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

## Acceptance Criteria

- [ ] `ThePredictionsBackup` database exists with matching schema
- [ ] `BackupWriter` login has write access to backup database
- [ ] `BACKUP_CONNECTION_STRING` GitHub secret is configured
- [ ] GitHub Actions workflow runs daily at 2am UTC
- [ ] Workflow can also be triggered manually via `workflow_dispatch`
- [ ] Backup contains all data from `TableCopyOrder` tables (no anonymisation)
- [ ] Token tables (`AspNetUserTokens`, `RefreshTokens`, `PasswordResetTokens`) are excluded

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 0 | [Manual Setup](./00-manual-setup.md) | Create backup database, login, schema, and GitHub secret | Not Started |
| 1 | [GitHub Actions Workflow](./01-github-actions-workflow.md) | Create the scheduled backup workflow | Not Started |
| 2 | [Verify and Go Live](./02-verify-and-go-live.md) | Trigger manually, verify data, confirm schedule | Not Started |

## Dependencies

- [x] `ProductionBackup` mode already implemented in `DatabaseRefresher.cs`
- [x] `PROD_CONNECTION_STRING` GitHub secret already exists
- [x] Existing `refresh-dev-db.yml` workflow as a template
- [ ] Fasthosts control panel access to create database and login

## Technical Notes

### No Code Changes Required

The `ProductionBackup` mode is already fully implemented in `tools/ThePredictions.DatabaseTools/`. It:
- Reads all `TableCopyOrder` tables from production
- Skips anonymisation, test account creation, and personal data verification
- Writes to the target database using `SqlBulkCopy`

### Token Tables Excluded

The `TablesToSkip` tables (`AspNetUserTokens`, `RefreshTokens`, `PasswordResetTokens`) are excluded from the backup. This is acceptable because:
- `AspNetUserTokens` is always empty (the application uses custom token tables instead)
- `RefreshTokens` and `PasswordResetTokens` contain short-lived tokens that regenerate on login

### Security

This backup contains real personal data. Treat the `BackupWriter` credentials and `BACKUP_CONNECTION_STRING` secret with the same care as production credentials.

## Related

- [`tools/ThePredictions.DatabaseTools/`](../../../../tools/ThePredictions.DatabaseTools/) — the database tools project (shared with dev refresh)
- [`.github/workflows/refresh-dev-db.yml`](../../../../.github/workflows/refresh-dev-db.yml) — the dev refresh workflow (similar pattern)
