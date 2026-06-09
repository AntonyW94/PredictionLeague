# GitHub Actions CI/CD Pipeline Plan for The Predictions

## Status

Not Started | **In Progress** | Complete

> **Verified June 2026:** Largely shipped. `.github/workflows/` has `ci.yml` (build + test + coverage), `deploy-dev.yml`, `deploy-prod.yml`, `refresh-dev-db.yml`, and `backup-prod-db.yml`. Outstanding: an E2E test workflow and a test-DB seeder tool.

## Overview

Complete CI/CD pipeline using GitHub Actions with four workflows:
1. **CI** - Build and test on every push/PR
2. **Deploy** - Manual deployment to production and development via FTP
3. **Dev DB Refresh** - Database refresh with anonymisation (already implemented: `refresh-dev-db.yml`)
4. **E2E Tests** - Playwright end-to-end testing

Additionally, the **Backup Production Database** workflow already exists (`backup-prod-db.yml`).

All workflows run on the **free tier** (2,000 minutes/month for private repos).

---

## Configuration Summary

| Setting | Value |
|---------|-------|
| Platform | GitHub Actions |
| Cost | Free (within 2,000 mins/month) |
| Runner | `ubuntu-latest` |
| .NET Version | 8.0 |
| FTP Deployment | SamKirkland/FTP-Deploy-Action |
| Browser Testing | Playwright (Chromium) |

---

## Required GitHub Secrets

Configure in: **Repository → Settings → Secrets and variables → Actions → New repository secret**

### Already configured

| Secret Name | Description | Used by |
|-------------|-------------|---------|
| `PROD_CONNECTION_STRING` | Production DB (read via `Refresh` login) | Dev refresh, Prod backup |
| `DEV_CONNECTION_STRING` | Dev DB (write via `RefreshDev` login) | Dev refresh |
| `BACKUP_CONNECTION_STRING` | Backup DB (write via `PredictionBackup` login) | Prod backup |
| `TEST_ACCOUNT_PASSWORD` | Password for test accounts created after anonymisation | Dev refresh |

### Still needed (for deploy and E2E workflows)

| Name | Type | Description | Used by |
|------|------|-------------|---------|
| `FTP_SERVER` | Variable | `ftp.fasthosts.co.uk` | Deploy |
| `DEV_FTP_USERNAME` | Secret | FTP username for dev site (`dev.thepredictions.co.uk`) | Deploy |
| `DEV_FTP_PASSWORD` | Secret | FTP password for dev site | Deploy |
| `DEV_APPSETTINGS_SECRETS` | Secret | Content of `appsettings.Development.Secrets.json` | Deploy |
| `PROD_FTP_USERNAME` | Secret | FTP username for production site (`thepredictions.co.uk`) | Deploy |
| `PROD_FTP_PASSWORD` | Secret | FTP password for production site | Deploy |
| `PROD_APPSETTINGS_SECRETS` | Secret | Content of `appsettings.Production.Secrets.json` | Deploy |

**Note:** `FTP_SERVER` is a GitHub Actions **variable** (not a secret) since the hostname is not sensitive. Configure variables in: **Repository → Settings → Secrets and variables → Actions → Variables**.

---

## Workflow Files

### 1. CI Workflow (`.github/workflows/ci.yml`)

Runs on every push and pull request to main branch.

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main, master]
  pull_request:
    branches: [main, master]

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore ThePredictions.sln

      - name: Build solution
        run: dotnet build ThePredictions.sln --no-restore --configuration Release /p:TreatWarningsAsErrors=true

      - name: Run Domain tests
        run: dotnet test tests/ThePredictions.Domain.Tests --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=domain-results.trx"

      - name: Run Validator tests
        run: dotnet test tests/ThePredictions.Validators.Tests --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=validator-results.trx"

      - name: Run Application tests
        run: dotnet test tests/ThePredictions.Application.Tests --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=application-results.trx"

      - name: Run Infrastructure tests
        run: dotnet test tests/ThePredictions.Infrastructure.Tests --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=infrastructure-results.trx"

      - name: Run API tests
        run: dotnet test tests/ThePredictions.API.Tests --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=api-results.trx"

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: '**/*-results.trx'
          retention-days: 7
```

---

### 2. Deploy Workflows (`.github/workflows/deploy-dev.yml`, `deploy-prod.yml`) ✅ Dev IMPLEMENTED

Separate workflows for each environment. Dev is implemented; production will be added after successful testing.

**Important:** The `.pubxml` publish profiles are tracked in git and define per-environment settings: `EnvironmentName` (written to `web.config`), and `CopyToPublishDirectory="Never"` exclusions for wrong-environment config files. The deploy workflow uses `-p:PublishProfile=Development` (or `Production`) so these settings are the single source of truth.

**Deployment strategy:**
- Uses `app_offline.htm` to gracefully take the site offline before deployment (IIS stops the app and serves a maintenance page)
- Uses `dangerous-clean-slate: true` to wipe all files and upload a fresh set (prevents stale DLL issues)
- Writes `appsettings.*.Secrets.json` into the publish output from a GitHub Secret (since `dangerous-clean-slate` deletes all server files including Secrets.json)
- Once deployment completes, the absence of `app_offline.htm` in the publish output causes IIS to restart the app with the new files

See the actual file (`deploy-dev.yml`) for the current dev implementation.

The plan below shows the future production workflow (`deploy-prod.yml`):

```yaml
# .github/workflows/deploy.yml (future: production + development)
name: Deploy

on:
  workflow_dispatch:
    inputs:
      environment:
        description: 'Target environment'
        required: true
        type: choice
        options:
          - production
          - development
      confirm:
        description: 'Type "deploy" to confirm deployment'
        required: true
        type: string

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - name: Validate confirmation
        if: github.event.inputs.confirm != 'deploy'
        run: |
          echo "Deployment cancelled. You must type 'deploy' to confirm."
          exit 1

  build-test-deploy:
    needs: validate
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore ThePredictions.sln

      - name: Build solution
        run: dotnet build ThePredictions.sln --no-restore --configuration Release

      - name: Run all tests
        run: dotnet test ThePredictions.sln --no-build --configuration Release --verbosity normal

      - name: Publish Web application
        run: |
          if [ "${{ github.event.inputs.environment }}" = "production" ]; then
            dotnet publish src/ThePredictions.Web/ThePredictions.Web.csproj --configuration Release --output ./publish --no-build -p:PublishProfile=Production
          else
            dotnet publish src/ThePredictions.Web/ThePredictions.Web.csproj --configuration Release --output ./publish --no-build -p:PublishProfile=Development
          fi

      - name: Deploy to Fasthosts via FTP
        uses: SamKirkland/FTP-Deploy-Action@v4.3.5
        with:
          server: ${{ vars.FTP_SERVER }}
          username: ${{ github.event.inputs.environment == 'production' && secrets.PROD_FTP_USERNAME || secrets.DEV_FTP_USERNAME }}
          password: ${{ github.event.inputs.environment == 'production' && secrets.PROD_FTP_PASSWORD || secrets.DEV_FTP_PASSWORD }}
          local-dir: ./publish/
          server-dir: /htdocs/
          dangerous-clean-slate: true
          exclude: |
            appsettings.${{ github.event.inputs.environment == 'production' && 'Production' || 'Development' }}.Secrets.json

      - name: Deployment complete
        run: |
          if [ "${{ github.event.inputs.environment }}" = "production" ]; then
            echo "Deployment to production complete!"
            echo "Site: https://www.thepredictions.co.uk"
          else
            echo "Deployment to development complete!"
            echo "Site: https://dev.thepredictions.co.uk"
          fi
```

**Note:** The `*.Secrets.json` files are gitignored and not in the repository. The deploy workflows write them from GitHub Secrets (`DEV_APPSETTINGS_SECRETS` / `PROD_APPSETTINGS_SECRETS`) into the publish output before FTP upload.

---

### 3. Dev Database Refresh Workflow (`.github/workflows/refresh-dev-db.yml`) ✅ IMPLEMENTED

Already implemented and working. Manual trigger only (no schedule). See the actual file for the current implementation.

The original plan below included a schedule trigger; the implemented version uses manual trigger only:

```yaml
# .github/workflows/refresh-dev-db.yml
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
          dotnet-version: '8.0.x'

      - name: Restore tool dependencies
        run: dotnet restore tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj

      - name: Build refresh tool
        run: dotnet build tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj --configuration Release

      - name: Run Database Refresh
        run: dotnet run --project tools/ThePredictions.DatabaseTools --configuration Release
        env:
          PROD_CONNECTION_STRING: ${{ secrets.PROD_CONNECTION_STRING }}
          DEV_CONNECTION_STRING: ${{ secrets.DEV_CONNECTION_STRING }}
          TEST_ACCOUNT_PASSWORD: ${{ secrets.TEST_ACCOUNT_PASSWORD }}

      - name: Report Success
        if: success()
        run: |
          echo "✅ Dev database refreshed successfully!"
          echo ""
          echo "Test accounts created:"
          echo "  • testplayer@dev.local / [password in secrets]"
          echo "  • testadmin@dev.local / [password in secrets]"

      - name: Report Failure
        if: failure()
        run: |
          echo "❌ Dev database refresh failed!"
          echo "Check the logs above for details."
```

---

### 4. E2E Tests Workflow (`.github/workflows/e2e.yml`)

Runs after successful CI on main branch, or manually triggered.

```yaml
# .github/workflows/e2e.yml
name: E2E Tests

on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [main, master]
  workflow_dispatch:

jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    if: ${{ github.event_name == 'workflow_dispatch' || github.event.workflow_run.conclusion == 'success' }}

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build solution
        run: dotnet build ThePredictions.sln --configuration Release

      # Cache Playwright browsers
      - name: Cache Playwright browsers
        uses: actions/cache@v4
        id: playwright-cache
        with:
          path: ~/.cache/ms-playwright
          key: playwright-${{ runner.os }}-${{ hashFiles('**/ThePredictions.E2E.Tests.csproj') }}

      - name: Install Playwright browsers
        if: steps.playwright-cache.outputs.cache-hit != 'true'
        run: |
          dotnet tool install --global Microsoft.Playwright.CLI
          playwright install --with-deps chromium

      - name: Install Playwright dependencies (cached)
        if: steps.playwright-cache.outputs.cache-hit == 'true'
        run: |
          dotnet tool install --global Microsoft.Playwright.CLI
          playwright install-deps chromium

      # Setup test database
      - name: Setup test database
        run: dotnet run --project tools/ThePredictions.TestDbSeeder --configuration Release
        env:
          TEST_DB_PATH: ./test-e2e.db
          TEST_ACCOUNT_PASSWORD: ${{ secrets.TEST_ACCOUNT_PASSWORD }}

      # Publish and start application
      - name: Publish application
        run: dotnet publish src/ThePredictions.Web/ThePredictions.Web.csproj --configuration Release --output ./publish

      - name: Start application
        run: |
          cd ./publish
          nohup dotnet ThePredictions.Web.dll --urls "http://localhost:5000" > ../app.log 2>&1 &
          echo $! > ../app.pid
        env:
          ASPNETCORE_ENVIRONMENT: Testing
          ConnectionStrings__DefaultConnection: "DataSource=../test-e2e.db"

      - name: Wait for application to start
        run: |
          echo "Waiting for application to start..."
          for i in {1..60}; do
            if curl -s http://localhost:5000/health > /dev/null 2>&1; then
              echo "✅ Application is ready!"
              exit 0
            fi
            echo "  Attempt $i/60..."
            sleep 2
          done
          echo "❌ Application failed to start"
          cat app.log
          exit 1

      # Run E2E tests
      - name: Run E2E tests
        run: dotnet test tests/ThePredictions.E2E.Tests --configuration Release --logger "trx;LogFileName=e2e-results.trx"
        env:
          E2E_BASE_URL: http://localhost:5000
          E2E_TEST_USER_EMAIL: testplayer@dev.local
          E2E_TEST_USER_PASSWORD: ${{ secrets.TEST_ACCOUNT_PASSWORD }}
          E2E_ADMIN_EMAIL: testadmin@dev.local
          E2E_ADMIN_PASSWORD: ${{ secrets.TEST_ACCOUNT_PASSWORD }}
          PLAYWRIGHT_BROWSERS_PATH: ~/.cache/ms-playwright

      # Upload results
      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: e2e-test-results
          path: '**/e2e-results.trx'
          retention-days: 7

      - name: Upload Playwright traces on failure
        uses: actions/upload-artifact@v4
        if: failure()
        with:
          name: playwright-traces
          path: |
            tests/ThePredictions.E2E.Tests/bin/Release/net8.0/playwright-traces/
            tests/ThePredictions.E2E.Tests/screenshots/
          retention-days: 7

      - name: Show application logs on failure
        if: failure()
        run: |
          echo "=== Application Logs ==="
          cat app.log || echo "No logs found"

      # Cleanup
      - name: Stop application
        if: always()
        run: |
          if [ -f app.pid ]; then
            kill $(cat app.pid) 2>/dev/null || true
          fi
```

---

## Workflow Triggers Summary

| Workflow | Automatic Trigger | Manual Trigger | Schedule | Status |
|----------|-------------------|----------------|----------|--------|
| **CI** | Push/PR to main | - | - | ✅ Implemented |
| **Deploy to Dev** | - | ✅ (requires "deploy" confirmation) | - | ✅ Implemented |
| **Deploy to Production** | - | ✅ (requires "deploy" confirmation) | - | Not started |
| **DB Refresh** | - | ✅ (requires "refresh" confirmation) | - | ✅ Implemented |
| **Prod Backup** | - | ✅ | Daily 2am UTC | ✅ Implemented |
| **E2E Tests** | After CI success on main | ✅ | - | Not started |

---

## Time Budget (Free Tier: 2,000 mins/month)

| Workflow | Duration | Frequency | Monthly Minutes |
|----------|----------|-----------|-----------------|
| CI | ~10 mins | 5/day × 22 days | ~1,100 mins |
| Deploy | ~6 mins | 4/month | ~24 mins |
| DB Refresh | ~5 mins | 4/month | ~20 mins |
| Prod Backup | ~5 mins | 30/month (daily) | ~150 mins |
| E2E Tests | ~12 mins | 8/month | ~96 mins |
| **Total** | | | **~1,390 mins** |

**Plenty of headroom within the 2,000 minute free tier.**

---

## Setup Instructions

### Step 1: Create GitHub Secrets

1. Go to your repository on GitHub
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add each secret from the table above

### Step 2: Create Workflow Files

Create the remaining workflow files:

```
.github/
└── workflows/
    ├── backup-prod-db.yml   ← ✅ Already exists
    ├── refresh-dev-db.yml   ← ✅ Already exists
    ├── ci.yml               ← ✅ Already exists
    ├── deploy-dev.yml       ← ✅ Already exists
    ├── deploy-prod.yml      ← To create
    └── e2e.yml              ← To create
```

### Step 3: Create Required Tools

```
tools/
├── ThePredictions.DatabaseTools/    ← ✅ Already exists (refresh + backup)
│   ├── Program.cs
│   └── ThePredictions.DatabaseTools.csproj
│
└── ThePredictions.TestDbSeeder/   ← To create (E2E test seeder)
    ├── Program.cs
    └── ThePredictions.TestDbSeeder.csproj
```

### Step 4: Verify Setup

1. **Test CI**: Push a commit or create a PR — verify build and tests run
2. **Test Deploy**: Go to Actions → Deploy → Run workflow → Select environment → Type "deploy"
3. **Test DB Refresh**: ✅ Already verified and working
4. **Test Prod Backup**: ✅ Already verified and working
5. **Test E2E**: Go to Actions → E2E Tests → Run workflow

---

## Deployment Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Deployment Flow                                    │
│                                                                              │
│  Developer pushes to main                                                    │
│         │                                                                    │
│         ▼                                                                    │
│  ┌─────────────────┐                                                        │
│  │   CI Workflow   │  ← Automatic                                           │
│  │  Build + Test   │                                                        │
│  └────────┬────────┘                                                        │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────┐                                                        │
│  │  E2E Workflow   │  ← Automatic (if CI passes)                            │
│  │   Playwright    │                                                        │
│  └────────┬────────┘                                                        │
│           │                                                                  │
│           ▼                                                                  │
│  Developer reviews results                                                   │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────┐                                                        │
│  │ Deploy Workflow │  ← Manual trigger (type "deploy")                      │
│  │   FTP Upload    │                                                        │
│  └────────┬────────┘                                                        │
│           │                                                                  │
│           ▼                                                                  │
│  Site is live at https://www.thepredictions.co.uk                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Weekly Maintenance Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Weekly Maintenance (Automated)                        │
│                                                                              │
│  Monday 6:00 AM UTC                                                          │
│         │                                                                    │
│         ▼                                                                    │
│  ┌─────────────────────────────────────┐                                    │
│  │   Dev DB Refresh Workflow           │                                    │
│  │   1. Connect to prod DB             │                                    │
│  │   2. Copy all data                  │                                    │
│  │   3. Anonymise personal data        │                                    │
│  │   4. Insert to dev DB               │                                    │
│  │   5. Add test accounts              │                                    │
│  └─────────────────────────────────────┘                                    │
│         │                                                                    │
│         ▼                                                                    │
│  Dev database is ready with fresh data                                       │
│  Test accounts: testplayer@dev.local, testadmin@dev.local                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Comparison: GitHub Actions vs Azure DevOps

| Feature | GitHub Actions | Azure DevOps |
|---------|---------------|--------------|
| Code location | GitHub ✅ | Requires connection |
| Setup complexity | Simple | More complex |
| YAML syntax | Clean | More verbose |
| Free tier | 2,000 mins/month | 1,800 mins/month |
| Secrets management | Built-in | Variable groups |
| Scheduling | Built-in cron | Built-in cron |
| Approval gates | Environment protection | Environments |
| Learning curve | Lower | Higher |

**Decision: GitHub Actions** - simpler setup, code already on GitHub, sufficient free tier.

---

## Migrating from Azure DevOps Plan

If you had started with the Azure DevOps plan:

1. **Delete** `azure-pipelines.yml` (if created)
2. **Remove** Azure DevOps service connections (if configured)
3. **Keep** Azure Key Vault for production secrets (accessed by app, not pipeline)
4. **Add** secrets to GitHub repository settings instead

---

## Future Enhancements

| Enhancement | Description | Priority |
|-------------|-------------|----------|
| Branch protection | Require CI to pass before merge | High |
| Code coverage | Add coverlet and upload reports | Medium |
| Slack/Teams notifications | Notify on deploy success/failure | Medium |
| Staging environment | Deploy to staging before prod | Low (dev site now exists) |
| Dependabot | Automated dependency updates | Low |

---

## Troubleshooting

### CI fails on build

- Check for build warnings (TreatWarningsAsErrors is enabled)
- Verify all NuGet packages are restored

### FTP deployment fails

- Verify FTP credentials in secrets
- Check Fasthosts FTP is accessible
- Verify server directory path (`/htdocs/`)

### E2E tests fail

- Check application logs in artifacts
- Review Playwright traces for failures
- Verify test account credentials match secrets

### DB refresh fails

- Verify connection strings in secrets
- Check network access to Fasthosts SQL Server
- Review logs for specific table errors

---

## Files to Create

| File | Description | Status |
|------|-------------|--------|
| `.github/workflows/ci.yml` | CI workflow | ✅ Implemented |
| `.github/workflows/deploy-dev.yml` | Deploy to dev site | ✅ Implemented |
| `.github/workflows/deploy-prod.yml` | Deploy to production site | Not started |
| `.github/workflows/refresh-dev-db.yml` | Database refresh workflow | ✅ Implemented |
| `.github/workflows/backup-prod-db.yml` | Production backup workflow | ✅ Implemented |
| `.github/workflows/e2e.yml` | E2E test workflow | Not started |
| `tools/ThePredictions.DatabaseTools/` | Database tools (refresh/backup) | ✅ Implemented |
| `tools/ThePredictions.TestDbSeeder/` | E2E test database seeder | Not started |
