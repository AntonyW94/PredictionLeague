# Database Schema - ThePredictions

This document describes the SQL Server database schema for the PredictionLeague application.

## Entity Relationship Overview

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Seasons   │────<│   Rounds    │────<│   Matches   │
└─────────────┘     └─────────────┘     └─────────────┘
       │                   │                   │
       │                   │                   │
       ▼                   ▼                   ▼
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Leagues   │     │RoundResults │     │UserPredictns│
└─────────────┘     └─────────────┘     └─────────────┘
       │                                       │
       │                                       │
       ▼                                       │
┌─────────────┐     ┌─────────────┐            │
│LeagueMembers│────>│ AspNetUsers │<───────────┘
└─────────────┘     └─────────────┘
       │                   ▲
       │                   │
       ▼                   │
┌─────────────┐     ┌─────────────┐
│LeagueMember │     │  Winnings   │
│   Stats     │     └─────────────┘
└─────────────┘            │
                           │
                           ▼
                    ┌─────────────┐
                    │LeaguePrize  │
                    │  Settings   │
                    └─────────────┘
```

---

## Core Domain Tables

### Seasons

Represents a football season or competition period.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Name | nvarchar(50) | NO | | Season name (e.g., "Premier League 2025/26") |
| IsActive | bit | NO | 1 | Whether season is currently active |
| NumberOfRounds | int | NO | 0 | Total rounds in the season |
| ApiLeagueId | int | YES | | External API league identifier |
| StartDateUtc | datetime2 | NO | | Season start date |
| EndDateUtc | datetime2 | NO | | Season end date |
| CompetitionType | int | NO | 0 | Type of competition (0 = League, etc.) |

**Constraints:**
- PK: `Id`
- UNIQUE: `Name`

---

### Rounds

Represents a gameweek within a season.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| SeasonId | int | NO | | FK to Seasons |
| RoundNumber | int | NO | | Round number within season |
| Status | nvarchar(50) | NO | 'Draft' | Draft, Published, InProgress, Completed |
| ApiRoundName | nvarchar(128) | YES | | External API round name |
| StartDateUtc | datetime2 | NO | | Round start date |
| DeadlineUtc | datetime2 | NO | | Prediction deadline |
| CompletedDateUtc | datetime2 | YES | | When round was completed |
| LastReminderSentUtc | datetime2 | YES | | Last reminder email sent |
| LastReminderSent | datetime2 | YES | | (Legacy column) |

**Constraints:**
- PK: `Id`
- UNIQUE: `SeasonId, RoundNumber`
- FK: `SeasonId` → `Seasons.Id` (CASCADE DELETE)

---

### Matches

Individual fixtures within a round.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| RoundId | int | NO | | FK to Rounds |
| HomeTeamId | int | YES | | FK to Teams (null for placeholder) |
| AwayTeamId | int | YES | | FK to Teams (null for placeholder) |
| Status | nvarchar(50) | NO | 'Scheduled' | Scheduled, InProgress, Completed, etc. |
| ActualHomeTeamScore | int | YES | | Final home score |
| ActualAwayTeamScore | int | YES | | Final away score |
| ExternalId | int | YES | | External API match ID |
| MatchDateTimeUtc | datetime2 | NO | | Kick-off time |
| CustomLockTimeUtc | datetime2 | YES | | Per-match lock time (for tournaments) |
| PlaceholderHomeName | nvarchar(100) | YES | | e.g., "Winner Group A" |
| PlaceholderAwayName | nvarchar(100) | YES | | e.g., "Runner-up Group B" |

**Constraints:**
- PK: `Id`
- FK: `RoundId` → `Rounds.Id` (CASCADE DELETE)
- FK: `HomeTeamId` → `Teams.Id`
- FK: `AwayTeamId` → `Teams.Id`

---

### Teams

Football teams.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Name | nvarchar(100) | NO | | Full team name |
| ShortName | nvarchar(16) | NO | | Short display name |
| Abbreviation | nvarchar(3) | NO | | 3-letter code (e.g., "MUN") |
| LogoUrl | nvarchar(255) | YES | | Team logo URL |
| ApiTeamId | int | YES | | External API team ID |

**Constraints:**
- PK: `Id`
- UNIQUE: `Name`

---

## League Tables

### Leagues

User-created prediction leagues.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Name | nvarchar(150) | NO | | League name |
| SeasonId | int | NO | | FK to Seasons |
| AdministratorUserId | nvarchar(450) | NO | | FK to AspNetUsers (league owner) |
| EntryCode | nvarchar(10) | YES | | 6-char code to join league |
| Price | decimal(18,2) | NO | 0 | Entry fee |
| IsFree | bit | NO | 0 | Whether league is free to join |
| HasPrizes | bit | NO | 1 | Whether league has prizes |
| PrizeFundOverride | decimal(18,2) | YES | | Override calculated prize fund |
| PointsForExactScore | int | NO | 5 | Points for exact score prediction |
| PointsForCorrectResult | int | NO | 3 | Points for correct result only |
| CreatedAtUtc | datetime2 | NO | GETUTCDATE() | Creation timestamp |
| EntryDeadlineUtc | datetime2 | YES | | Deadline to join league |

**Constraints:**
- PK: `Id`
- UNIQUE: `SeasonId, Name`
- FK: `SeasonId` → `Seasons.Id` (CASCADE DELETE)
- FK: `AdministratorUserId` → `AspNetUsers.Id`

---

### LeagueMembers

Junction table for users in leagues.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| LeagueId | int | NO | | FK to Leagues |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| Status | nvarchar(20) | NO | 'Pending' | Pending, Approved, Rejected |
| IsAlertDismissed | bit | NO | 0 | UI state for alerts |
| JoinedAtUtc | datetime2 | NO | GETUTCDATE() | When user requested to join |
| ApprovedAtUtc | datetime2 | YES | | When membership was approved |

**Constraints:**
- PK: `LeagueId, UserId` (composite)
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

---

### LeagueMemberStats

Cached ranking statistics per member per league.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| LeagueId | int | NO | | FK to Leagues |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| OverallRank | int | NO | 0 | Current overall rank |
| MonthRank | int | NO | 0 | Current month rank |
| LiveRoundRank | int | NO | 0 | Live updating round rank |
| SnapshotOverallRank | int | NO | 0 | Pre-round overall rank |
| SnapshotMonthRank | int | NO | 0 | Pre-round month rank |
| StableRoundRank | int | NO | 0 | Round rank after completion |
| LiveRoundPoints | decimal(10,2) | NO | 0.00 | Live round points |
| StableRoundPoints | decimal(10,2) | NO | 0.00 | Final round points |

**Constraints:**
- PK: `LeagueId, UserId` (composite)
- FK: `LeagueId` → `Leagues.Id`
- FK: `UserId` → `AspNetUsers.Id`

---

### LeagueRoundResults

Per-user, per-round, per-league results (includes boost effects).

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeagueId | int | NO | | FK to Leagues |
| RoundId | int | NO | | FK to Rounds |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| BasePoints | int | NO | | Points before boost |
| BoostedPoints | int | NO | | Points after boost applied |
| HasBoost | bit | NO | 0 | Whether boost was used |
| AppliedBoostCode | nvarchar(50) | YES | | Which boost was applied |

**Constraints:**
- PK: `Id`
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `RoundId` → `Rounds.Id`
- FK: `UserId` → `AspNetUsers.Id`

---

## Prize Tables

### LeaguePrizeSettings

Prize configuration per league.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeagueId | int | NO | | FK to Leagues |
| PrizeType | nvarchar(20) | NO | | Overall, Monthly, Round, MostExactScores |
| Rank | int | NO | | Prize position (1st, 2nd, 3rd, etc.) |
| PrizeAmount | money | NO | | Prize amount |
| PrizeDescription | nvarchar(255) | YES | | Display text (e.g., "1st Place") |

**Constraints:**
- PK: `Id`
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)

**Prize Types:**
- `Overall` - End of season position prizes
- `Monthly` - Monthly aggregate prizes
- `Round` - Weekly round winner prizes
- `MostExactScores` - Most exact predictions prize

---

### Winnings

Actual prize payouts to users.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| LeaguePrizeSettingId | int | NO | | FK to LeaguePrizeSettings |
| Amount | decimal(18,2) | NO | | Prize amount awarded |
| RoundNumber | int | YES | | For Round prizes |
| Month | int | YES | | For Monthly prizes (1-12) |
| AwardedDateUtc | datetime2 | NO | GETUTCDATE() | When prize was awarded |

**Constraints:**
- PK: `Id`
- FK: `UserId` → `AspNetUsers.Id`
- FK: `LeaguePrizeSettingId` → `LeaguePrizeSettings.Id`

---

## Prediction Tables

### UserPredictions

User score predictions for matches.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| MatchId | int | NO | | FK to Matches |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| PredictedHomeScore | int | NO | | Predicted home team score |
| PredictedAwayScore | int | NO | | Predicted away team score |
| PointsAwarded | int | YES | | Points earned (null if not scored) |
| Outcome | int | NO | 0 | 0=NotScored, 1=Exact, 2=Correct, 3=Incorrect |
| CreatedAtUtc | datetime2 | NO | GETUTCDATE() | First prediction time |
| UpdatedAtUtc | datetime2 | YES | | Last update time |

**Constraints:**
- PK: `Id`
- UNIQUE: `MatchId, UserId`
- FK: `MatchId` → `Matches.Id` (CASCADE DELETE)
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

---

### RoundResults

Aggregated results per user per round (across all leagues).

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| RoundId | int | NO | | FK to Rounds |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| TotalPoints | int | NO | 0 | Total points earned |
| ExactScoreCount | int | NO | 0 | Number of exact scores |
| CorrectResultCount | int | NO | 0 | Number of correct results |
| IncorrectCount | int | NO | 0 | Number of incorrect predictions |

**Constraints:**
- PK: `Id`
- UNIQUE: `RoundId, UserId`
- FK: `RoundId` → `Rounds.Id`
- FK: `UserId` → `AspNetUsers.Id`

---

## Boost Tables

### BoostDefinitions

Available boost types in the system.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Code | nvarchar(50) | NO | | Unique code (e.g., "DOUBLE_UP") |
| Name | nvarchar(100) | NO | | Display name |
| Description | nvarchar(255) | YES | | Boost description |
| Scope | nvarchar(20) | NO | | Round or Match scope |
| ImageUrl | nvarchar(255) | YES | | Normal state image |
| SelectedImageUrl | nvarchar(255) | YES | | Selected state image |
| DisabledImageUrl | nvarchar(255) | YES | | Disabled state image |
| Tooltip | nvarchar(255) | YES | | Hover tooltip text |

**Constraints:**
- PK: `Id`
- UNIQUE: `Code`

---

### LeagueBoostRules

Which boosts are enabled per league.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeagueId | int | NO | | FK to Leagues |
| BoostDefinitionId | int | NO | | FK to BoostDefinitions |
| TotalUsesPerSeason | int | NO | | Max uses per season |
| IsEnabled | bit | NO | 1 | Whether boost is active |

**Constraints:**
- PK: `Id`
- UNIQUE: `LeagueId, BoostDefinitionId`
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `BoostDefinitionId` → `BoostDefinitions.Id`

---

### LeagueBoostWindows

Usage windows restricting when boosts can be used.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeagueBoostRuleId | int | NO | | FK to LeagueBoostRules |
| StartRoundNumber | int | NO | | Window start round |
| EndRoundNumber | int | NO | | Window end round |
| MaxUsesInWindow | int | NO | | Max uses in this window |

**Constraints:**
- PK: `Id`
- FK: `LeagueBoostRuleId` → `LeagueBoostRules.Id` (CASCADE DELETE)

---

### UserBoostUsages

Tracks when users have used boosts.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| LeagueId | int | NO | | FK to Leagues |
| SeasonId | int | NO | | FK to Seasons |
| RoundId | int | YES | | FK to Rounds (for round-scope boosts) |
| MatchId | int | YES | | FK to Matches (for match-scope boosts) |
| BoostDefinitionId | int | NO | | FK to BoostDefinitions |
| PlayedAtUtc | datetime2 | NO | GETUTCDATE() | When boost was applied |

**Constraints:**
- PK: `Id`
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `SeasonId` → `Seasons.Id`
- FK: `RoundId` → `Rounds.Id`
- FK: `MatchId` → `Matches.Id`
- FK: `BoostDefinitionId` → `BoostDefinitions.Id`

---

## Identity Tables (ASP.NET Core Identity)

### AspNetUsers

Extended ASP.NET Identity users table.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | nvarchar(450) | NO | | Primary key (GUID string) |
| UserName | nvarchar(256) | YES | | Username |
| NormalizedUserName | nvarchar(256) | YES | | Uppercase username |
| Email | nvarchar(256) | YES | | Email address |
| NormalizedEmail | nvarchar(256) | YES | | Uppercase email |
| EmailConfirmed | bit | NO | | Email verified |
| PasswordHash | nvarchar(max) | YES | | Hashed password |
| SecurityStamp | nvarchar(max) | YES | | Security stamp |
| ConcurrencyStamp | nvarchar(max) | YES | | Concurrency token |
| PhoneNumber | nvarchar(max) | YES | | Phone number |
| PhoneNumberConfirmed | bit | NO | | Phone verified |
| TwoFactorEnabled | bit | NO | | 2FA enabled |
| LockoutEnd | datetimeoffset | YES | | Lockout expiry |
| LockoutEnabled | bit | NO | | Lockout enabled |
| AccessFailedCount | int | NO | | Failed login attempts |
| **FirstName** | nvarchar(100) | NO | | **Custom: User's first name** |
| **LastName** | nvarchar(100) | NO | | **Custom: User's last name** |

**Constraints:**
- PK: `Id`

---

### AspNetRoles

Standard roles table.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | nvarchar(450) | NO | Primary key |
| Name | nvarchar(256) | YES | Role name |
| NormalizedName | nvarchar(256) | YES | Uppercase name |
| ConcurrencyStamp | nvarchar(max) | YES | Concurrency token |

---

### AspNetUserRoles

User-role junction table.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| UserId | nvarchar(450) | NO | FK to AspNetUsers |
| RoleId | nvarchar(450) | NO | FK to AspNetRoles |

**Constraints:**
- PK: `UserId, RoleId` (composite)

---

### AspNetUserClaims / AspNetRoleClaims / AspNetUserLogins / AspNetUserTokens

Standard ASP.NET Identity tables for claims, external logins, and tokens.

---

### RefreshTokens

JWT refresh tokens for authentication.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| Token | nvarchar(max) | NO | | Refresh token value |
| Expires | datetime2 | NO | | Expiry time |
| Created | datetime2 | NO | | Creation time |
| Revoked | datetime2 | YES | | When revoked (null if active) |

**Constraints:**
- PK: `Id`
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

---

## Common Queries Reference

### Get user's prize wins by type

```sql
SELECT
    lps.PrizeType,
    COUNT(*) AS WinCount,
    SUM(w.Amount) AS TotalWon
FROM Winnings w
JOIN LeaguePrizeSettings lps ON w.LeaguePrizeSettingId = lps.Id
WHERE w.UserId = @UserId AND lps.LeagueId = @LeagueId
GROUP BY lps.PrizeType
```

### Get remaining prizes (not yet won)

```sql
-- Round prizes remaining
SELECT COUNT(*)
FROM LeaguePrizeSettings lps
WHERE lps.LeagueId = @LeagueId
  AND lps.PrizeType = 'Round'
  AND NOT EXISTS (
    SELECT 1 FROM Winnings w
    WHERE w.LeaguePrizeSettingId = lps.Id
      AND w.RoundNumber IS NOT NULL
  )

-- Monthly prizes remaining
SELECT COUNT(DISTINCT lps.Id)
FROM LeaguePrizeSettings lps
WHERE lps.LeagueId = @LeagueId
  AND lps.PrizeType = 'Monthly'
  AND NOT EXISTS (
    SELECT 1 FROM Winnings w
    WHERE w.LeaguePrizeSettingId = lps.Id
  )
```

### Overall leaderboard for a league

```sql
SELECT
    u.FirstName + ' ' + LEFT(u.LastName, 1) AS PlayerName,
    SUM(lrr.BoostedPoints) AS TotalPoints,
    RANK() OVER (ORDER BY SUM(lrr.BoostedPoints) DESC) AS Rank
FROM LeagueMembers lm
JOIN AspNetUsers u ON lm.UserId = u.Id
LEFT JOIN LeagueRoundResults lrr ON lm.UserId = lrr.UserId AND lrr.LeagueId = @LeagueId
WHERE lm.LeagueId = @LeagueId AND lm.Status = 'Approved'
GROUP BY lm.UserId, u.FirstName, u.LastName
ORDER BY TotalPoints DESC
```
