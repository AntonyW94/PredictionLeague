# Task: Apply Schema to Dev Database

**Parent Feature:** [Development Database Refresh](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Apply the production database schema to the empty `ThePredictionsDev` database so it has all tables, constraints, and indexes ready to receive data.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| N/A (manual task) | N/A | Schema applied via SSMS or Azure Data Studio |

## Implementation Steps

### Step 1: Connect to Development Database

Connect to the dev database using SSMS or Azure Data Studio:

```
Server: mssql04.mssql.prositehosting.net
Database: ThePredictionsDev
User ID: RefreshDev
Password: THev_05c4r
```

### Step 2: Generate Schema Script from Production

Connect to the production database and generate a schema-only script:

1. In SSMS: Right-click `ThePredictions` → Tasks → Generate Scripts
2. Select "Script entire database and all database objects"
3. In Advanced options:
   - Types of data to script: **Schema only**
   - Script DROP and CREATE: **Script CREATE**
   - Script Indexes: **True**
   - Script Foreign Keys: **True**
   - Script Primary Keys: **True**
   - Script Unique Keys: **True**
   - Script Check Constraints: **True**
   - Script Defaults: **True**
4. Save to file

### Step 3: Run Schema Script Against Dev Database

1. Open the generated script
2. Change the database context to `ThePredictionsDev`
3. Execute the script
4. Verify all tables are created

### Step 4: Verify Table Count

Run this query against the dev database to verify all tables exist:

```sql
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME
```

Expected tables (25):
- AspNetRoleClaims
- AspNetRoles
- AspNetUserClaims
- AspNetUserLogins
- AspNetUserRoles
- AspNetUserTokens
- AspNetUsers
- BoostDefinitions
- LeagueBoostRules
- LeagueBoostWindows
- LeagueMemberStats
- LeagueMembers
- LeaguePrizeSettings
- LeagueRoundResults
- Leagues
- Matches
- PasswordResetTokens
- RefreshTokens
- RoundResults
- Rounds
- Seasons
- Teams
- UserBoostUsages
- UserPredictions
- Winnings

## Verification

- [ ] Can connect to `ThePredictionsDev` with dev credentials
- [ ] All 24 tables exist in the dev database
- [ ] All foreign key constraints are present
- [ ] All indexes and unique constraints are present
- [ ] Identity columns (IDENTITY) are set up correctly

## Edge Cases to Consider

- If the Fasthosts SQL Server version differs from production, some syntax may need adjusting
- If identity insert is used in the schema script, ensure `SET IDENTITY_INSERT` is handled correctly
- Some system tables or __EFMigrationsHistory may or may not be needed

## Notes

This is a one-time manual task. Once the schema is applied, the refresh tool (Task 3) will handle ongoing data synchronisation via truncate + insert.
