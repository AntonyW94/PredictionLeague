# Task: Run Initial Refresh and Verify

**Parent Feature:** [Development Database Refresh](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Manually trigger the first database refresh and verify that the dev database contains correctly anonymised data with working test accounts.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| N/A (manual verification task) | N/A | All verification done via GitHub Actions UI and database queries |

## Implementation Steps

### Step 1: Verify Prerequisites

Before triggering the first refresh, confirm:

- [ ] Dev database schema is applied (Task 1 complete)
- [ ] GitHub secrets are configured (Task 2 complete)
- [ ] Refresh tool code is merged to `master` (Task 3 complete)
- [ ] GitHub Actions workflow is merged to `master` (Task 4 complete)

### Step 2: Manually Trigger the Workflow

1. Go to the repository on GitHub
2. Click **Actions** tab
3. Select **Refresh Dev Database** workflow
4. Click **Run workflow**
5. Type `refresh` in the confirmation input
6. Click **Run workflow** button
7. Monitor the workflow run for success/failure

### Step 3: Verify Workflow Logs

Check each step in the workflow logs:

- [ ] .NET restore succeeded
- [ ] Build succeeded
- [ ] Tool connected to both databases
- [ ] Tables were read from production
- [ ] Data was anonymised
- [ ] Dev tables were populated
- [ ] Test accounts were created
- [ ] Personal data verification passed
- [ ] "Dev database refreshed successfully!" message appeared

### Step 4: Verify Anonymised Data in Dev Database

Connect to the dev database and run these verification queries:

```sql
-- Check total user count matches production
SELECT
    COUNT(*) AS UserCount
FROM
    [AspNetUsers] u

-- Verify no real emails remain (should return 1 — the preserved account only)
SELECT
    COUNT(*) AS RealEmailCount
FROM
    [AspNetUsers] u
WHERE
    u.[Email] NOT LIKE 'user%@testmail.com'
    AND u.[Email] NOT IN ('testplayer@dev.local', 'testadmin@dev.local', 'antony.willson@hotmail.com')

-- Verify preserved account exists with original data
SELECT
    u.[Id],
    u.[Email],
    u.[FirstName],
    u.[LastName]
FROM
    [AspNetUsers] u
WHERE
    u.[Email] = 'antony.willson@hotmail.com'

-- Verify all other names are anonymised
SELECT TOP 10
    u.[FirstName],
    u.[LastName],
    u.[Email]
FROM
    [AspNetUsers] u
WHERE
    u.[Email] != 'antony.willson@hotmail.com'

-- Verify refresh tokens are cleared
SELECT
    COUNT(*) AS TokenCount
FROM
    [RefreshTokens] rt
-- Expected: 0

-- Verify password reset tokens are cleared
SELECT
    COUNT(*) AS ResetTokenCount
FROM
    [PasswordResetTokens] prt
-- Expected: 0

-- Verify external logins only exist for preserved account
SELECT
    ul.*,
    u.[Email]
FROM
    [AspNetUserLogins] ul
JOIN [AspNetUsers] u
    ON ul.[UserId] = u.[Id]
-- Expected: only rows for antony.willson@hotmail.com

-- Verify leagues are anonymised
SELECT
    l.[Id],
    l.[Name],
    l.[EntryCode]
FROM
    [Leagues] l
```

### Step 5: Verify Test Accounts

```sql
-- Check test accounts exist
SELECT
    u.[Id],
    u.[Email],
    u.[FirstName],
    u.[LastName],
    u.[EmailConfirmed]
FROM
    [AspNetUsers] u
WHERE
    u.[Email] IN ('testplayer@dev.local', 'testadmin@dev.local')

-- Check admin has Admin role
SELECT
    u.[Email],
    r.[Name] AS RoleName
FROM
    [AspNetUsers] u
JOIN [AspNetUserRoles] ur
    ON u.[Id] = ur.[UserId]
JOIN [AspNetRoles] r
    ON ur.[RoleId] = r.[Id]
WHERE
    u.[Email] = 'testadmin@dev.local'

-- Check both are in a league
SELECT
    u.[Email],
    l.[Name] AS LeagueName,
    lm.[Status]
FROM
    [LeagueMembers] lm
JOIN [AspNetUsers] u
    ON lm.[UserId] = u.[Id]
JOIN [Leagues] l
    ON lm.[LeagueId] = l.[Id]
WHERE
    u.[Email] IN ('testplayer@dev.local', 'testadmin@dev.local')
```

### Step 6: Test Login with Test Accounts

If the API can be pointed at the dev database:

1. Update local `appsettings.Development.json` to use the dev connection string
2. Run the API locally
3. Attempt to log in as `testplayer@dev.local` with password from `TEST_ACCOUNT_PASSWORD` secret
4. Attempt to log in as `testadmin@dev.local` with password from `TEST_ACCOUNT_PASSWORD` secret
5. Verify both logins succeed and return valid JWT tokens

## Verification

- [ ] Workflow completed successfully in GitHub Actions
- [ ] Dev database has the same number of rows as production (minus deleted tokens/logins)
- [ ] No real email addresses remain in `AspNetUsers`
- [ ] No real names remain in `AspNetUsers`
- [ ] All password hashes are invalidated (except test accounts)
- [ ] `RefreshTokens` table is empty
- [ ] `AspNetUserLogins` table is empty
- [ ] `AspNetUserTokens` table is empty
- [ ] League names are anonymised
- [ ] Entry codes are regenerated
- [ ] Test player account exists and is in a league
- [ ] Test admin account exists, has Admin role, and is in a league
- [ ] Test accounts can log in with the known password

## Edge Cases to Consider

- If the workflow fails, check the logs for connection timeouts (Fasthosts may block GitHub IPs)
- If row counts don't match, check for tables that were intentionally skipped
- If test login fails, verify the password hasher version matches what the API expects

## Notes

After the first successful refresh, subsequent refreshes can be triggered manually via the GitHub Actions "Run workflow" button whenever a fresh copy of production data is needed.
