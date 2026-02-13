# Task 2: Verify and Go Live

**Parent Feature:** [Daily Production Database Backup](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Verify the backup workflow works end-to-end by triggering it manually, checking the backup data, and confirming the daily schedule runs successfully.

## Checklist

- [ ] Trigger workflow manually and confirm it succeeds
- [ ] Verify backup database contains expected data
- [ ] Confirm daily schedule runs overnight
- [ ] Mark feature as Complete

---

## Step 1: Trigger Workflow Manually

1. Go to the GitHub repository: Actions > "Backup Production Database"
2. Click "Run workflow" (the `workflow_dispatch` trigger)
3. Wait for the workflow to complete
4. Check the logs — you should see output like:

```
[INFO] Starting database refresh...
[INFO] Anonymisation: disabled
[INFO] Reading [AspNetRoles] from source...
...
[INFO] Database refresh completed.
Production database backed up successfully!
```

### If the Workflow Fails

Common issues:
- **"BACKUP_CONNECTION_STRING environment variable is not set or is empty"** — Check the GitHub secret is configured correctly
- **"Login failed for user 'PredictionBackup'"** — Check the login credentials in the connection string
- **"Invalid object name '[TableName]'"** — The schema hasn't been applied to the backup database (see [Task 0](./00-manual-setup.md))

---

## Step 2: Verify Backup Data

Connect to `ThePredictionsBackup` using SSMS or Azure Data Studio and run these checks:

### Check Row Counts Match Production

```sql
-- Run this on BOTH ThePredictions and ThePredictionsBackup and compare
SELECT
    t.[name] AS [Table],
    p.[rows] AS [RowCount]
FROM
    sys.tables t
INNER JOIN
    sys.partitions p
    ON t.[object_id] = p.[object_id]
WHERE
    p.[index_id] IN (0, 1)
ORDER BY
    t.[name]
```

### Verify Token Tables Are Empty

```sql
SELECT COUNT(*) AS [Count] FROM [AspNetUserTokens]    -- Expected: 0
SELECT COUNT(*) AS [Count] FROM [RefreshTokens]         -- Expected: 0
SELECT COUNT(*) AS [Count] FROM [PasswordResetTokens]   -- Expected: 0
```

### Verify Data Is NOT Anonymised

```sql
-- Should return real email addresses (not @testmail.com)
SELECT TOP 5
    u.[Email]
FROM
    [AspNetUsers] u
```

---

## Step 3: Confirm Daily Schedule

After the manual run succeeds, wait for the next 2am UTC run and check:

1. Go to Actions > "Backup Production Database"
2. Confirm a scheduled run appears and completed successfully
3. Check the run was triggered by "schedule" (not "workflow_dispatch")

---

## Step 4: Mark Feature as Complete

Once both manual and scheduled runs are verified:

1. Update the README status from `In Progress` to `Complete`
2. Tick off the acceptance criteria in the README

## Notes

- The first manual run will take longer than subsequent runs because it needs to build the .NET project from scratch (no cache)
- Subsequent runs will be faster due to GitHub Actions caching
- If the backup fails overnight, GitHub will send email notifications to repository admins by default
