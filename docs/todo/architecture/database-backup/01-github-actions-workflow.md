# Task 1: GitHub Actions Workflow

**Parent Feature:** [Daily Production Database Backup](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a GitHub Actions workflow that runs the `ProductionBackup` mode on a daily schedule and supports manual triggering.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `.github/workflows/backup-prod-db.yml` | Create | Scheduled backup workflow |

## Implementation Steps

### Step 1: Create the Workflow File

Create `.github/workflows/backup-prod-db.yml` following the same pattern as `refresh-dev-db.yml`:

```yaml
name: Backup Production Database

on:
  schedule:
    - cron: '0 2 * * *' # Daily at 2am UTC
  workflow_dispatch:

jobs:
  backup:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore tool dependencies
        run: dotnet restore tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj

      - name: Build backup tool
        run: dotnet build tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj --configuration Release

      - name: Run Production Backup
        run: dotnet run --project tools/ThePredictions.DatabaseTools --configuration Release -- ProductionBackup
        env:
          PROD_CONNECTION_STRING: ${{ secrets.PROD_CONNECTION_STRING }}
          BACKUP_CONNECTION_STRING: ${{ secrets.BACKUP_CONNECTION_STRING }}

      - name: Report Success
        if: success()
        run: echo "Production database backed up successfully!"
```

### Key Differences from `refresh-dev-db.yml`

| Aspect | Dev Refresh | Production Backup |
|--------|-------------|-------------------|
| Trigger | `workflow_dispatch` with confirmation input | `schedule` (cron) + `workflow_dispatch` (no confirmation) |
| Mode | `DevelopmentRefresh` | `ProductionBackup` |
| Target DB secret | `DEV_CONNECTION_STRING` | `BACKUP_CONNECTION_STRING` |
| Test password | Required (`TEST_ACCOUNT_PASSWORD`) | Not needed |
| Confirmation gate | Yes (`if: confirm == 'refresh'`) | No (safe to run automatically) |

## Code Patterns to Follow

Follow the existing `refresh-dev-db.yml` workflow:
- Same `actions/checkout@v4` and `actions/setup-dotnet@v4` versions
- Same restore → build → run sequence
- Same `.csproj` path and `--configuration Release` flag
- Connection strings passed as environment variables from secrets

## Verification

- [ ] Workflow file is valid YAML
- [ ] Cron expression `0 2 * * *` matches "daily at 2am UTC"
- [ ] `workflow_dispatch` allows manual triggering from the Actions tab
- [ ] Only `PROD_CONNECTION_STRING` and `BACKUP_CONNECTION_STRING` secrets are referenced
- [ ] Mode argument is `ProductionBackup` (not `DevelopmentRefresh`)

## Notes

- No confirmation input is needed for the backup workflow (unlike the dev refresh) because it writes to a dedicated backup database and is safe to run automatically
- GitHub's default email notifications will alert repository admins if the workflow fails
