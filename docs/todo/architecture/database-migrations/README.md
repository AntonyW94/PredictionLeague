# Database Migrations System (DbUp)

## Status

Not Started | In Progress | **Complete** (delivered on branch `database-migrations-dbup`; see [ADR-0013](../../../decisions/0013-database-migrations-dbup.md). NB: the split `deploy-dev.yml` / `deploy-prod.yml` exist, not a single `deploy.yml` as drafted below.)

## Summary

Implement version-controlled database schema changes using DbUp, integrated into the existing `ThePredictions.DatabaseTools` project and GitHub Actions workflows. Migrations must run against all three databases (production, backup, dev) since the current `DatabaseRefresher` is a data-only copy tool that does not touch the schema.

## Priority

**High** - Manual schema changes across 3 databases is unsustainable

## Problem

The current `DatabaseRefresher` reads rows from a source database, truncates the target, and bulk-inserts. It does not touch the schema. This means if a column is added to production, the backup and dev databases won't have that column and the next data copy will fail.

## Solution

Add [DbUp](https://dbup.readthedocs.io/) to the existing `ThePredictions.DatabaseTools` project. DbUp runs numbered SQL migration scripts and tracks which have been applied in a `SchemaVersions` table on each database independently.

## Requirements

- [ ] Add `dbup-sqlserver` NuGet package to `ThePredictions.DatabaseTools.csproj`
- [ ] Create `DatabaseMigrator.cs` that runs embedded SQL scripts via DbUp
- [ ] Add `Migrate` mode to `Program.cs` that accepts `MIGRATE_CONNECTION_STRING`
- [ ] Create `Migrations/` folder for numbered SQL scripts
- [ ] Create baseline migration from current schema
- [ ] Add migration step to `deploy.yml` (production)
- [ ] Add migration step to `backup-prod-db.yml` (backup)
- [ ] Add migration step to `refresh-dev-db.yml` (dev)
- [ ] Document rollback procedures

## What Changes

| File | Change |
|------|--------|
| `ThePredictions.DatabaseTools.csproj` | Add `dbup-sqlserver` NuGet package |
| `DatabaseMigrator.cs` (new) | Runs embedded SQL scripts via DbUp |
| `Program.cs` | Add `Migrate` mode that accepts `MIGRATE_CONNECTION_STRING` |
| `Migrations/` folder (new) | Numbered SQL scripts, e.g. `001-baseline.sql`, `002-add-user-preferences.sql` |

## Three Databases to Migrate

All three databases need schema changes applied before any data copy or code deployment:

| Database | Secret | When to migrate | Workflow |
|----------|--------|-----------------|----------|
| **Production** | `PROD_CONNECTION_STRING` | Before deploying new code | `deploy.yml` |
| **Backup** | `BACKUP_CONNECTION_STRING` | Before the daily data backup | `backup-prod-db.yml` |
| **Dev** | `DEV_CONNECTION_STRING` | Before the dev data refresh | `refresh-dev-db.yml` |

## Workflow Integration

Each workflow gets a migration step **before** its existing work:

```yaml
# deploy.yml - migrate prod before deploying new code
- name: Run database migrations (production)
  run: dotnet run --project tools/ThePredictions.DatabaseTools -- Migrate
  env:
    MIGRATE_CONNECTION_STRING: ${{ secrets.PROD_CONNECTION_STRING }}

# backup-prod-db.yml - migrate backup before copying data
- name: Run database migrations (backup)
  run: dotnet run --project tools/ThePredictions.DatabaseTools -- Migrate
  env:
    MIGRATE_CONNECTION_STRING: ${{ secrets.BACKUP_CONNECTION_STRING }}

# refresh-dev-db.yml - migrate dev before copying data
- name: Run database migrations (dev)
  run: dotnet run --project tools/ThePredictions.DatabaseTools -- Migrate
  env:
    MIGRATE_CONNECTION_STRING: ${{ secrets.DEV_CONNECTION_STRING }}
```

DbUp's `SchemaVersions` table tracks which scripts have run on each database independently, so it is safe to run against all three. Each database only applies the scripts it hasn't seen yet.

## Why DbUp

- The codebase uses Dapper + raw SQL, so writing migration scripts in SQL is consistent
- The `tools/` console app pattern already exists and runs in GitHub Actions
- All three connection string secrets are already configured
- DbUp is simple: no ORM, no code-first models, just numbered SQL files

## Technical Notes

```csharp
// DatabaseMigrator.cs
var upgrader = DeployChanges.To
    .SqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
```

## Dependencies

- Requires `deploy.yml` to exist first (roadmap item #2) for production migrations
- Existing `backup-prod-db.yml` and `refresh-dev-db.yml` workflows will need updating
