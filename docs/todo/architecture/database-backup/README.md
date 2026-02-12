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
| Backup DB Login | TBD (create in Fasthosts, e.g. `BackupWriter`) |
| Schedule | Daily (e.g. 2am UTC) |
| Anonymisation | None — this is an exact copy |
| Tool | `tools/ThePredictions.DatabaseTools/` (mode: `ProductionBackup`) |

## Prerequisites

- [ ] Create `ThePredictionsBackup` database in Fasthosts
- [ ] Create a login for the backup database (e.g. `BackupWriter`)
- [ ] Apply schema to backup database
- [ ] Add `BACKUP_CONNECTION_STRING` GitHub secret

## Notes

- This contains real personal data — treat the backup DB credentials with the same care as production
- The existing `Refresh` login on prod can be reused for reading
- Uses the same `ThePredictions.DatabaseTools` project as the dev refresh, with `ProductionBackup` mode (skips anonymisation and test accounts)
- The backup workflow will run on a daily schedule (GitHub Actions cron), unlike the dev refresh which is manual-only
- Full plan to be written when ready to implement

## Related

- [Dev Database Refresh](../dev-database-refresh/README.md) — similar tool with anonymisation
