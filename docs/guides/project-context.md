# Project Context (Reference)

This file provides background context for understanding the codebase. Unlike other files in this directory, this is **reference material** - not rules to actively apply.

## What This Application Does

**The Predictions** is a sports prediction league application where users:
- Predict football match scores
- Compete in leagues with friends
- Win prizes based on prediction accuracy

## Technology Stack

| Category | Technology | Version |
|----------|-----------|---------|
| Platform | .NET | 8.0 |
| Backend | ASP.NET Core | 8.0 |
| Frontend | Blazor WebAssembly | 8.0 |
| Database | SQL Server | - |
| ORM | Dapper | 2.1.66 |
| CQRS/Mediator | MediatR | 14.0.0 |
| Validation | FluentValidation | 12.1.1 |
| Guard Clauses | Ardalis.GuardClauses | 5.0.0 |
| Authentication | JWT + Google OAuth + ASP.NET Identity | 8.0 |
| Email | Brevo (Sendinblue) | 1.1.1 |
| Logging | Serilog + Datadog | 8.0.3 |
| Secrets | Azure Key Vault | 1.4.0 |

## Solution Structure

```
src/
├── PredictionLeague.API              → REST controllers and endpoints
├── PredictionLeague.Application      → CQRS commands, queries, and handlers
├── PredictionLeague.Contracts        → DTOs shared between layers
├── PredictionLeague.Domain           → Core business entities and domain logic
├── PredictionLeague.Hosting.Shared   → Shared hosting configuration
├── PredictionLeague.Infrastructure   → Data access, repositories, external services
├── PredictionLeague.Validators       → FluentValidation validators
├── PredictionLeague.Web              → Blazor server host
└── PredictionLeague.Web.Client       → Blazor WebAssembly UI components
tests/                                → (planned)
tools/
└── ThePredictions.DatabaseTools      → Database refresh (dev) and backup (prod) tool
```

**Dependency Direction:** Presentation → Application → Domain (never reverse)

## Domain Concepts

### League

- Container for users competing together
- Has members, prize settings, entry code
- Administrator is automatically added as approved member on creation
- Entry codes are 6-character alphanumeric strings

### Round

- Represents a gameweek with multiple matches
- Status flow: `Draft` → `Published` → `InProgress` → `Completed`
- Has a deadline (`DeadlineUtc`) after which predictions are locked

### Match

- Individual fixture within a round
- Has home/away teams and scores
- Future: `CustomLockTimeUtc` for per-match deadlines (column exists, not yet used)

### Prediction

- User's predicted score for a match
- Can be edited unlimited times before deadline
- Scoring:
  - **Exact Score** - Predicted score matches actual score exactly
  - **Correct Result** - Predicted winner/draw matches actual result
- Points configurable per league (`PointsForExactScore`, `PointsForCorrectResult`)

### Prize Distribution

Four prize strategies:
- `RoundPrizeStrategy` - Weekly winners
- `MonthlyPrizeStrategy` - Monthly aggregate winners
- `OverallPrizeStrategy` - Season-end winners
- `MostExactScoresPrizeStrategy` - Most exact predictions

### Boosts

Power-ups that modify scoring:
- **Double Up** - Doubles all points for the round it's applied to
- Configured per league with usage limits and windows

## Infrastructure

### Environments

| Environment | Site URL | Database | Key Vault |
|-------------|----------|----------|-----------|
| Production | `https://www.thepredictions.co.uk` | `ThePredictions` | `the-predictions-prod` |
| Development | `https://dev.thepredictions.co.uk` | `ThePredictionsDev` | `the-predictions-dev` |
| Local | `https://localhost:7132` | Uses dev Key Vault | `the-predictions-dev` |

There is also a backup database (`ThePredictionsBackup`) which receives a daily unmodified copy of production data.

### Hosting

- **Provider:** Fasthosts shared hosting (both sites)
- **Access:** FTP only (no RDP, no server configuration)
- **Deployment:** Manual publish from Visual Studio, then upload via CuteFTP
- **FTP hostname:** `ftp.fasthosts.co.uk` for both sites

### Database Server

All databases are hosted on `mssql04.mssql.prositehosting.net` (Fasthosts SQL Server).

| Database | Purpose | Logins |
|----------|---------|--------|
| `ThePredictions` | Production | `AntonyWillson` (app), `Refresh` (read for backups/refresh) |
| `ThePredictionsDev` | Development | `AntonyWillsonDev` (app), `RefreshDev` (read for refresh) |
| `ThePredictionsBackup` | Daily backup of production | `PredictionBackup` (write) |

All database logins have database owner permissions (Fasthosts only supports owner or no access).

### Publish Profiles

The `PredictionLeague.Web` project has two publish profiles:

| Profile | Environment | Output Folder | Notes |
|---------|-------------|---------------|-------|
| `Publish to Production` | `Production` | `bin\Release\net8.0\publish\` | Excludes dev config files |
| `Publish to Development` | `Development` | `bin\Release\net8.0\publish-dev\` | Excludes prod config files |

Each profile uses `CopyToPublishDirectory="Never"` to exclude the other environment's `appsettings` and secrets files. The `EnvironmentName` property in each `.pubxml` controls the `ASPNETCORE_ENVIRONMENT` value written into the published `web.config`.

### Configuration Files

| File | Used by | Purpose |
|------|---------|---------|
| `appsettings.json` | All environments | Shared base config with Key Vault substitution placeholders |
| `appsettings.Local.json` | Local development | Localhost URLs, dev Key Vault URI |
| `appsettings.Development.json` | Hosted dev site | Dev site URLs, dev Key Vault URI |
| `appsettings.Production.json` | Live site | Production URLs, prod Key Vault URI |
| `appsettings.Production.Secrets.json` | Live site | Azure service principal credentials (not in source control) |
| `appsettings.Development.Secrets.json` | Hosted dev site | Azure service principal credentials (not in source control) |

The `*.Secrets.json` files contain Azure AD service principal credentials (`TenantId`, `ClientId`, `ClientSecret`) used to authenticate to Key Vault. They are gitignored and must be manually placed on each Fasthosts site.

`launchSettings.json` sets the environment to `Local` for Visual Studio debugging, which loads `appsettings.Local.json` and uses `DefaultAzureCredential` (Visual Studio sign-in) for Key Vault access.

### Azure Key Vault

Both Key Vaults use **RBAC (role-based access control)** for permissions.

Each environment has its own Key Vault and Azure AD App Registration (service principal):

| Environment | Key Vault | App Registration |
|-------------|-----------|------------------|
| Production | `the-predictions-prod` | `The Predictions (Prod)` |
| Development | `the-predictions-dev` | `The Predictions (Dev)` |

Each App Registration has the **Key Vault Secrets User** role on its respective Key Vault.

Secrets are referenced in `appsettings.json` using substitution syntax: `${Secret-Name}`. The `EnableSubstitutions()` call in `Program.cs` replaces these placeholders with actual Key Vault values at startup.

### Scheduled Jobs (via cron-job.org)

Production only. The development site does not have scheduled jobs.

| Job | Frequency | Endpoint |
|-----|-----------|----------|
| Publish Upcoming Rounds | Daily at 9am | `/api/tasks/publish-upcoming-rounds` |
| Send Email Reminders | Every 30 minutes | `/api/tasks/send-reminders` |
| Sync Season | Daily at 8am | `/api/tasks/sync-season` |
| Live Update Scores | Every minute | `/api/tasks/update-live-scores` |

All scheduled endpoints protected by API key (`X-Api-Key` header).

### GitHub Actions

| Workflow | File | Trigger | Purpose |
|----------|------|---------|---------|
| Refresh Dev Database | `refresh-dev-db.yml` | Manual only | Copies production data to dev with anonymisation |
| Backup Production Database | `backup-prod-db.yml` | Daily at 2am UTC + manual | Copies production data to backup (no anonymisation) |

Both workflows use `tools/ThePredictions.DatabaseTools/`. The **dev refresh** reads all tables from production, anonymises personal data (realistic fake names/emails via Bogus), creates test accounts (`testplayer@dev.local` and `testadmin@dev.local`), and writes to the dev database. The **production backup** copies all data unmodified to `ThePredictionsBackup` as a safety net independent of Fasthosts' own backup policy. Token tables (`AspNetUserTokens`, `RefreshTokens`, `PasswordResetTokens`) are excluded from both.

#### GitHub Secrets

| Secret | Used by | Description |
|--------|---------|-------------|
| `PROD_CONNECTION_STRING` | Both workflows | Reads from production using `Refresh` login |
| `DEV_CONNECTION_STRING` | Dev refresh | Writes to dev using `RefreshDev` login |
| `BACKUP_CONNECTION_STRING` | Prod backup | Writes to backup using `PredictionBackup` login |
| `TEST_ACCOUNT_PASSWORD` | Dev refresh | Password for test accounts created after anonymisation |

### External API

- **Provider:** api-sports.io (Football API)
- **No fallback data** — app relies entirely on API availability

## Design Decisions

These are intentional trade-offs, not issues to fix:

1. **Dapper over EF Core** — Chosen for performance and explicit SQL control
2. **Blazor WASM** — Client-side rendering for responsiveness, tokens in localStorage
3. **MediatR** — Decouples controllers from business logic
4. **No unit tests yet** — Planned for future implementation
5. **Manual FTP deployment** — Hosting limitation (CI/CD via GitHub Actions planned)
6. **Separate environments** — Local (localhost), Development (hosted dev site), Production

## Documentation Locations

| Topic | Location |
|-------|----------|
| Coding guides | [`/docs/guides/`](.) |
| Workflow checklists | [`/docs/guides/checklists/`](checklists/) |
| Database schema | [`/docs/guides/database-schema.md`](database-schema.md) |
| CSS reference | [`/docs/guides/css-reference.md`](css-reference.md) |
| Security accepted risks | [`/docs/security/accepted-risks.md`](../security/accepted-risks.md) |
| Security audit history | [`/docs/security/audit-history.md`](../security/audit-history.md) |
| Feature plans | [`/docs/todo/features/`](../todo/features/) |
| Architecture plans | [`/docs/todo/architecture/`](../todo/architecture/) |
| Security plans | [`/docs/todo/security/`](../todo/security/) |

## Useful Commands

```bash
# Run the API
dotnet run --project src/PredictionLeague.API

# Run the Blazor client
dotnet run --project src/PredictionLeague.Web

# Build all projects
dotnet build PredictionLeague.sln
```
