# Task 0: Manual Setup (Prerequisites)

**Parent Feature:** [Daily Production Database Backup](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Complete all manual infrastructure setup before the backup workflow can run. This includes creating the backup database, a dedicated login, applying the schema, and adding the connection string as a GitHub secret.

## Checklist

- [ ] Create `ThePredictionsBackup` database
- [ ] Create `BackupWriter` login
- [ ] Apply schema to backup database
- [ ] Add `BACKUP_CONNECTION_STRING` GitHub secret

---

## Step 1: Create `ThePredictionsBackup` Database

1. Log in to the Fasthosts control panel
2. Create a new SQL Server database named `ThePredictionsBackup` on the same server as `ThePredictions`

---

## Step 2: Create `BackupWriter` Login

1. In Fasthosts, create a new SQL login named `BackupWriter`
2. Grant it the following roles on `ThePredictionsBackup`:
   - `db_datareader` — read data (for constraint checks)
   - `db_datawriter` — write data
   - `db_ddladmin` — manage constraints (the tool disables/re-enables foreign keys during the copy)

---

## Step 3: Apply Schema to Backup Database

Connect to `ThePredictionsBackup` using SSMS or Azure Data Studio and create all tables with the same schema as production.

The tool expects **all** of the following tables to exist (it manages constraints on all of them, even those it doesn't copy data into):

### Tables that receive data (`TableCopyOrder`)

These tables are truncated and repopulated on every backup run:

1. `AspNetRoles`
2. `AspNetUsers`
3. `AspNetUserRoles`
4. `AspNetUserClaims`
5. `AspNetRoleClaims`
6. `AspNetUserLogins`
7. `Teams`
8. `Seasons`
9. `Rounds`
10. `Matches`
11. `BoostDefinitions`
12. `Leagues`
13. `LeagueMembers`
14. `LeagueMemberStats`
15. `LeagueBoostRules`
16. `LeagueBoostWindows`
17. `LeaguePrizeSettings`
18. `UserPredictions`
19. `RoundResults`
20. `LeagueRoundResults`
21. `UserBoostUsages`
22. `Winnings`

### Tables that must exist but stay empty (`TablesToSkip`)

These tables are truncated but no data is copied into them. They must exist because the tool disables/re-enables their foreign key constraints:

1. `AspNetUserTokens`
2. `RefreshTokens`
3. `PasswordResetTokens`

### How to Apply the Schema

The easiest approach is to script the production database schema:

1. In SSMS, right-click the `ThePredictions` database
2. Tasks > Generate Scripts
3. Select all tables (including indexes and foreign keys)
4. Script to a new query window
5. Change the database context to `ThePredictionsBackup`
6. Run the script

### Verify Schema

After applying, verify the table count matches:

```sql
SELECT
    COUNT(*) AS [TableCount]
FROM
    INFORMATION_SCHEMA.TABLES t
WHERE
    t.[TABLE_TYPE] = 'BASE TABLE'
```

Expected result: **25** tables (22 in `TableCopyOrder` + 3 in `TablesToSkip`).

---

## Step 4: Add `BACKUP_CONNECTION_STRING` GitHub Secret

1. Go to the GitHub repository: Settings > Secrets and variables > Actions
2. Click "New repository secret"
3. Name: `BACKUP_CONNECTION_STRING`
4. Value: the connection string using the `BackupWriter` login, pointing at `ThePredictionsBackup`

Example format:

```
Server=your-server;Database=ThePredictionsBackup;User Id=BackupWriter;Password=your-password;TrustServerCertificate=True
```

---

## Troubleshooting

### Schema Script Issues

**Error: "There is already an object named '...'"**
- A table already exists. Drop it first or use `IF NOT EXISTS` checks.

**Error: "Foreign key constraint references invalid table"**
- Tables must be created in the correct order. The generated script from SSMS should handle this automatically.

### Login Issues

**Error: "Login failed for user 'BackupWriter'"**
- Check the login was created in Fasthosts and has access to `ThePredictionsBackup`
- Verify the password in the connection string matches

---

## Next Steps

Once all manual setup is complete, proceed to:
1. [Task 1: GitHub Actions Workflow](./01-github-actions-workflow.md)
2. [Task 2: Verify and Go Live](./02-verify-and-go-live.md)
