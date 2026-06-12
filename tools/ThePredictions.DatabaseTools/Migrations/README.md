# Database Migrations (DbUp)

Versioned schema migrations applied by `ThePredictions.DatabaseTools` in `Migrate` mode, using
[DbUp](https://dbup.readthedocs.io/). See [ADR-0013](../../../docs/decisions/0013-database-migrations-dbup.md)
for the full rationale.

## How it works

- Every `*.sql` file in this folder is an **embedded resource** (see the `.csproj`).
- DbUp runs them in **alphabetical order**, which - because of the naming convention below - is
  also chronological/numeric order.
- DbUp records which scripts have run in a **`dbo.SchemaVersions`** table **per database**, so the
  same set can be run against production, dev and backup safely; each database only applies what
  it has not seen. A run with nothing pending is a no-op.
- Each script runs in its **own transaction** with a 5-minute command timeout (DDL headroom).

## Naming convention

```
NNNN_PascalCaseDescription.sql
```

- `NNNN` is a zero-padded, monotonically increasing number (`0001`, `0002`, …). Zero-padding keeps
  alphabetical order == execution order.
- Example: `0001_Baseline.sql`, `0002_AddUserPreferences.sql`.

## Rules

1. **Applied migrations are immutable.** Once a script may have run anywhere, never edit it - add a
   new numbered script. Editing an applied script does **not** re-run it (DbUp keys on the script
   name) and silently diverges environments.
2. **Make scripts idempotent / guarded** (`IF [NOT] EXISTS …`) where practical, so they are safe if
   re-pointed at a database that already has the change.
3. **Forward-only.** DbUp does not roll back. To undo a change, add a new forward script that
   reverses it. Before a destructive migration, run `backup-prod-db.yml` first.
4. **Match the column order of a `SELECT`/record only where relevant** - this folder is schema, not
   queries; but remember any new table/column must also be reflected in
   [`docs/guides/database-schema.md`](../../../docs/guides/database-schema.md) and the
   `DatabaseTools` copy/anonymise arrays (per the root `CLAUDE.md`).

## Running

```bash
# Applies all pending migrations to the database in MIGRATE_CONNECTION_STRING.
MIGRATE_CONNECTION_STRING="<connection string>" \
  dotnet run --project tools/ThePredictions.DatabaseTools -- Migrate
```

In CI this is driven by `.github/workflows/migrate-shared.yml` (and the `migrate-dev/-prod/-backup`
wrappers); the deploy / refresh / backup workflows call it before their own work.

## `0001_Baseline.sql`

The baseline is the full production schema as of 2026-06-12, generated via SMO and made fully
idempotent. It builds a brand-new database from zero **and** is a verified no-op on the existing
populated databases. `COLLATE` clauses are omitted, so a new database must be created with the
production default collation `SQL_Latin1_General_CP1_CI_AS`.
