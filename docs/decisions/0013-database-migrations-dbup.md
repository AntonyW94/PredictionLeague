# 0013. Database migrations with DbUp

- **Status:** Accepted
- **Date:** 2026-06-12
- **Deciders:** Antony Willson
- **Tags:** technical

## Context

Schema changes were applied to the three databases (production `ThePredictions`, development
`ThePredictionsDev`, backup `ThePredictionsBackup`) by hand. The `DatabaseRefresher` tool only
copies *data* (truncate + bulk-insert); it never touches schema. So a manual `ALTER` on
production never reached dev or backup, and the schemas had **drifted**: backup was missing
`AspNetUsers.TermsAcceptedAtUtc`, `AspNetUsers.MarketingOptInAtUtc` and
`LeagueMembers.IsArchivedByUser`, lacked `UQ_TournamentRoundMappings_Season_Round`, and had a
non-cascade `FK_TournamentRoundMappings_Seasons`. Manual, drift-prone schema management across
three databases is unsustainable.

[ADR-0009] anticipated tooling here. We adopt [DbUp](https://dbup.readthedocs.io/): numbered SQL
scripts, applied in order, tracked per-database in a `SchemaVersions` journal table. It fits the
existing Dapper + raw-SQL style and the existing `tools/ThePredictions.DatabaseTools` console-app
pattern that already runs in GitHub Actions.

This directly conflicts with the then-current **rule #10** ("never create `.sql` files in the
repository"), which existed precisely *because* no migration tool existed. That rationale no
longer holds, so the rule is amended rather than silently broken.

## Decision

**We will use DbUp, driven by `tools/ThePredictions.DatabaseTools` in a new `Migrate` mode.**

1. **Committed `.sql` migrations, scoped exception to rule #10.** Migration scripts live in
   `src/ThePredictions.Persistence.SqlServer/Migrations/` (moved there August 2026 by the
   persistence split, with the journal keys renamed on every database to match), are committed, and are marked
   `<EmbeddedResource>`. Naming is `NNNN_PascalCaseDescription.sql`, zero-padded, so alphabetical
   order is execution order. **This folder is the only place committed `.sql` files are allowed**;
   everywhere else rule #10 still stands (present ad-hoc SQL in chat). Applied migrations are
   immutable - never edit one; add a new script.

2. **Reconcile-first, then baseline from the live production schema.** Before writing the
   baseline, the true live schema of all three databases was diffed. The only drift was in
   backup (above); it was reconciled directly, and the one auto-named default constraint
   (`Rounds.DisplayName`) was renamed to the stable `DF_Rounds_DisplayName` on all three so that
   name-based idempotency guards work everywhere. `0001_Baseline.sql` was then generated from the
   real production schema (not from the schema doc), made **fully idempotent** (every object
   guarded with `IF [NOT] EXISTS`), so it builds a brand-new database from zero **and** is a true
   no-op on the three existing databases - verified against live dev: it created `SchemaVersions`,
   recorded the baseline, and altered **no** existing object; a second run was a clean no-op.
   The redundant SMO-generated FK "re-enable" statements were stripped, because they would
   execute (re-validate) on existing databases and require `ALTER` rights for what must be a
   no-op. Any real drift is fixed in follow-up scripts (`0002+`), never by editing the baseline.

3. **Reusable CI workflow + standalone per-environment dispatch** (mirrors EctManager ADR-0074,
   adapted to this repo's GitHub-hosted runners and repo-level secrets). `migrate-shared.yml`
   (`workflow_call`) runs the migrator; thin `workflow_dispatch` wrappers `migrate-dev.yml`,
   `migrate-prod.yml`, `migrate-backup.yml` map the existing `DEV_/PROD_/BACKUP_CONNECTION_STRING`
   secrets into it (so a schema change can be applied to one database without a code deploy).
   `deploy-dev.yml`, `deploy-prod.yml`, `refresh-dev-db.yml` and `backup-prod-db.yml` each gain a
   `migrate` job that `uses:` the shared workflow, with their main job declaring `needs: migrate`
   so schema always lands before code is deployed or data is copied. The connection string is
   passed as the `MIGRATE_CONNECTION_STRING` environment variable, never a command-line argument.
   **No new GitHub secrets.**

4. **Prod safety gate = typed confirmation (Option A).** This repo does not use GitHub
   Environments, and there is a single developer, so a required-reviewer gate is not applicable.
   `migrate-prod.yml` and `migrate-backup.yml` require typing `migrate` to confirm, matching the
   existing deploy workflows. (Adopting GitHub Environments with a reviewer gate is a possible
   future hardening step.)

5. **Forward-only; rollback via forward "undo" scripts + the backup database.** DbUp does not roll
   back. To reverse a change, add a new forward script that undoes it. Before any destructive
   migration, run `backup-prod-db.yml` first so the backup database is a current safety net.

## Consequences

**For / positive**
- One versioned, ordered, auditable source of truth for schema, applied identically to all three
  databases; drift cannot silently accumulate again.
- Schema can be applied to any one database without a code deploy (standalone wrappers), and is
  guaranteed to land before code/data in the deploy/refresh/backup paths.
- The idempotent baseline doubles as a from-zero build script for disaster recovery / new
  environments.

**Against / cost**
- More workflow files (one reusable + three wrappers) and a bumped `Microsoft.Data.SqlClient`
  (5.2.2 -> 6.1.4) in the tool to satisfy `dbup-sqlserver` 7.2.0. The bump is isolated to the
  standalone tooling project (not referenced by the app).
- A directly dispatchable `migrate-prod.yml` is a sharp tool; mitigated by the typed-confirm gate.

**Neutral / notes**
- **Collation:** `0001_Baseline.sql` omits `COLLATE` clauses, so a *new* database inherits its
  default collation. It **must** be created with `SQL_Latin1_General_CP1_CI_AS` (the production
  default) to match. No effect on the existing databases (the baseline is a no-op there).
- **Migration-login privileges:** the baseline needs only `CREATE TABLE` + `db_datawriter` (it
  creates `SchemaVersions` and journals; everything else is a guarded no-op on existing DBs).
  **Future `ALTER`-type migrations need `ALTER` on existing objects.** Verified: `RefreshDev`
  (dev) and `AntonyWillson` (prod) Key Vault logins have it; the prod `Refresh` login behind
  `PROD_CONNECTION_STRING` is unverified - confirm it (or repoint prod migrations at an
  `AntonyWillson`-based connection) before the first ALTER-type prod migration.
- **Coverage:** `ThePredictions.DatabaseTools` is outside the Domain 100% line/branch coverage
  gate (no test project references it; the pre-existing `DatabaseRefresher` is likewise
  uncovered and CI is green). `DatabaseMigrator` is therefore not unit-tested; it is verified
  behaviourally against live dev. It is still in the solution, so it must build warning-clean
  under `/p:TreatWarningsAsErrors=true`.

## Alternatives considered

- **Inline migration step inside each deploy workflow** (no reusable workflow) — rejected:
  migrations could then only run as part of a full deploy, but applying a schema change on its
  own is a real need.
- **Dedicated per-environment migration SQL logins** — rejected for now: the host
  (Fasthosts shared SQL) does not enforce least privilege the way a self-managed server would,
  so dedicated logins add credential-management overhead and new secrets without a real privilege
  gain. Revisit if the databases move to a host with granular permissions.
- **Keeping rule #10 and pasting baseline SQL in chat** — rejected: a 1,500-line baseline and an
  ongoing migration history must be version-controlled, not retyped from chat.

## Related

- [ADR-0009](0009-platform-and-data.md) — platform & data (anticipated this tooling).
- EctManager ADR-0074 (CI-driven migrations via a reusable workflow) — the pattern adapted here.
- `docs/todo/architecture/database-migrations/README.md` — the design doc.
- `src/ThePredictions.Persistence.SqlServer/Migrations/README.md` — script naming/conventions.
