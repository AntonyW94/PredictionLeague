# Task: Create GitHub Actions Workflow

**Parent Feature:** [Development Database Refresh](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a GitHub Actions workflow that supports manual triggering of the database refresh tool.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `.github/workflows/refresh-dev-db.yml` | Create | GitHub Actions workflow definition |

## Implementation Steps

### Step 1: Create the Workflow File

Create `.github/workflows/refresh-dev-db.yml`:

```yaml
name: Refresh Dev Database

on:
  workflow_dispatch:
    inputs:
      confirm:
        description: 'Type "refresh" to confirm database refresh'
        required: true
        type: string

jobs:
  refresh:
    runs-on: ubuntu-latest
    if: github.event.inputs.confirm == 'refresh'

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore tool dependencies
        run: dotnet restore tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj

      - name: Build refresh tool
        run: dotnet build tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj --configuration Release

      - name: Run Database Refresh
        run: dotnet run --project tools/ThePredictions.DatabaseTools --configuration Release -- DevelopmentRefresh
        env:
          PROD_CONNECTION_STRING: ${{ secrets.PROD_CONNECTION_STRING }}
          DEV_CONNECTION_STRING: ${{ secrets.DEV_CONNECTION_STRING }}
          TEST_ACCOUNT_PASSWORD: ${{ secrets.TEST_ACCOUNT_PASSWORD }}

      - name: Report Success
        if: success()
        run: |
          echo "Dev database refreshed successfully!"
          echo ""
          echo "Test accounts created:"
          echo "  testplayer@dev.local / [password in secrets]"
          echo "  testadmin@dev.local / [password in secrets]"

```

> **Failure notifications:** This workflow relies on GitHub's built-in notification system.
> Ensure you are "watching" the repository with Actions failure notifications enabled:
> GitHub → Repository → Watch → Custom → check "Workflows".

### Step 2: Verify Workflow Syntax

Ensure the YAML is valid and the workflow triggers are correct:

- `workflow_dispatch` trigger: manual only, with confirmation input
- `if` condition: only runs when "refresh" is typed

## Code Patterns to Follow

Follow the existing CI/CD workflow patterns in the repository (if any exist under `.github/workflows/`).

## Verification

- [ ] YAML syntax is valid (GitHub will flag errors on push)
- [ ] Workflow appears in GitHub Actions tab after push
- [ ] Manual trigger works via "Run workflow" button
- [ ] Confirmation input ("refresh") is required for manual trigger
- [ ] .NET 8 SDK is set up correctly
- [ ] Environment variables are passed from secrets
- [ ] Workflow shows success/failure status clearly

## Edge Cases to Consider

- If the refresh tool fails, the workflow should fail with a clear error message
- If secrets are not configured, the tool should fail fast with a helpful error
- The `workflow_dispatch` confirmation prevents accidental manual triggers

## Notes

- The `workflow_dispatch` input requiring "refresh" is a safety mechanism to prevent accidental database overwrites
- The workflow runs on `ubuntu-latest` which has network access to the Fasthosts SQL Server
- If Fasthosts blocks GitHub Actions IP ranges, we may need to explore alternatives (self-hosted runner, Azure Functions, etc.)
