# Development Database Refresh

## Status

**Not Started** | In Progress | Complete

## Overview

This document covers the setup of a development database and the automated refresh process that copies anonymised data from production.

| Component | Value |
|-----------|-------|
| Production Database | Fasthosts SQL Server (`PredictionLeague`) |
| Development Database | Fasthosts SQL Server (`PredictionLeague_Dev`) |
| Refresh Schedule | Weekly (Monday 6am UTC) |
| Refresh Tool | `tools/PredictionLeague.DevDbRefresh/` |

---

## Phase 1: Fasthosts Database Setup

| Step | Who | Task | Details |
|------|-----|------|---------|
| 1.1 | **You** | Log into Fasthosts control panel | https://www.fasthosts.co.uk |
| 1.2 | **You** | Create dev database | Go to SQL Server → Create new database → Name: `PredictionLeague_Dev` |
| 1.3 | **You** | Note the dev connection string | Copy the connection string for the new database |
| 1.4 | **You** | Run schema on dev database | Use SSMS or Azure Data Studio to run your schema creation scripts against the new dev DB |

---

## Phase 2: GitHub Secrets

Add these secrets to the GitHub repository:

| Secret | Description | Example |
|--------|-------------|---------|
| `PROD_CONNECTION_STRING` | Production SQL connection string | `Server=sql.fasthosts.co.uk;Database=PredictionLeague;...` |
| `DEV_CONNECTION_STRING` | Dev SQL connection string | `Server=sql.fasthosts.co.uk;Database=PredictionLeague_Dev;...` |
| `TEST_ACCOUNT_PASSWORD` | Password for test accounts | `TestPassword123!` |

---

## Phase 3: Database Refresh Tool

Create the tool at `tools/PredictionLeague.DevDbRefresh/`

### Project File

**File:** `tools/PredictionLeague.DevDbRefresh/PredictionLeague.DevDbRefresh.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Bogus" Version="35.6.1" />
    <PackageReference Include="Dapper" Version="2.1.66" />
    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.2.0" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
  </ItemGroup>
</Project>
```

### Refresh Process

The tool performs these steps:

1. Connect to Production DB (read-only)
2. Read all tables in dependency order
3. Anonymise PII in memory (emails, names, league names)
4. Truncate Dev DB tables
5. Insert anonymised data to Dev DB
6. Add known test accounts
7. Verify no real PII remains

### Data Anonymisation

| Data Type | Original | Anonymised |
|-----------|----------|------------|
| Emails | john.smith@gmail.com | user123@testmail.com |
| Names | John Smith | TestUser47 Player |
| League names | Smith Family League | Manchester Predictions |
| Entry codes | ABC123 | XYZ789 (regenerated) |
| Passwords | (hashed) | INVALIDATED |
| Refresh tokens | (deleted) | (deleted) |

---

## Phase 4: Test Accounts

The refresh tool creates these fixed test accounts:

| Account | Email | Role | Notes |
|---------|-------|------|-------|
| Test Player | `testplayer@dev.local` | User | Regular user for testing |
| Test Admin | `testadmin@dev.local` | Admin | Admin user for testing |

Both accounts use the password stored in `TEST_ACCOUNT_PASSWORD` secret.

These accounts are:
- Added to the first league automatically
- Available immediately after DB refresh
- Used by Playwright E2E tests
- Used for manual testing

---

## Phase 5: Initial Database Refresh

| Step | Who | Task | Details |
|------|-----|------|---------|
| 5.1 | **You** | Verify secrets are set | Double-check all secrets in GitHub |
| 5.2 | **You** | Merge PR with tools | Merge the code Claude creates |
| 5.3 | **You** | Manually trigger DB refresh | Actions → Refresh Dev Database → Run workflow |
| 5.4 | **You** | Verify refresh succeeded | Check workflow logs |
| 5.5 | **You** | Verify test accounts exist | Connect to dev DB and check `testplayer@dev.local` and `testadmin@dev.local` exist |

---

## GitHub Actions Workflow

**File:** `.github/workflows/refresh-dev-db.yml`

```yaml
name: Refresh Dev Database

on:
  schedule:
    - cron: '0 6 * * 1'  # Every Monday at 6:00 AM UTC
  workflow_dispatch:
    inputs:
      confirm:
        description: 'Type "refresh" to confirm database refresh'
        required: true
        default: 'refresh'
        type: string

jobs:
  refresh:
    runs-on: ubuntu-latest
    if: github.event_name == 'schedule' || github.event.inputs.confirm == 'refresh'

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore tool dependencies
        run: dotnet restore tools/PredictionLeague.DevDbRefresh/PredictionLeague.DevDbRefresh.csproj

      - name: Build refresh tool
        run: dotnet build tools/PredictionLeague.DevDbRefresh/PredictionLeague.DevDbRefresh.csproj --configuration Release

      - name: Run Database Refresh
        run: dotnet run --project tools/PredictionLeague.DevDbRefresh --configuration Release
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

---

## Verification Checklist

- [ ] Dev database created in Fasthosts
- [ ] Schema applied to dev database
- [ ] GitHub secrets configured (PROD_CONNECTION_STRING, DEV_CONNECTION_STRING, TEST_ACCOUNT_PASSWORD)
- [ ] DevDbRefresh tool created
- [ ] GitHub workflow created
- [ ] First manual refresh completed
- [ ] Test accounts verified in dev database
- [ ] Automated weekly refresh scheduled

---

## Related Documentation

- [CI/CD Plan](../ci-cd/README.md) - GitHub Actions workflow details
- [Staging Environment](../staging-environment/README.md) - Uses the dev database
- [Test Suite Plan](../test-suite/README.md) - E2E tests use the test accounts
