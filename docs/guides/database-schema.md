# Database Schema - ThePredictions

This document describes the SQL Server database schema for the ThePredictions application.

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

### Competitions

Reference data for a competition: the stable, provider-independent identity that seasons belong to (ADR 0009). Holds the competition type and the external API league id (both moved off `Seasons`), plus an optional logo URL. The `ApiLeagueId` is admin-editable so the fixture provider can be repointed without a deploy and without changing the competition's `Id` (which keeps reward entitlements and price comparables intact).

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Code | nvarchar(50) | NO | | Stable slug (e.g., "EPL", "WORLD_CUP") |
| Name | nvarchar(200) | NO | | Competition name (e.g., "Premier League") |
| Type | int | NO | | Type of competition (0 = League, 1 = Tournament) |
| LogoUrl | nvarchar(500) | YES | | External logo URL (admin-entered) |
| Description | nvarchar(max) | YES | | Admin-entered format/rules blurb shown on the Season Pass acquire page |
| ApiLeagueId | int | YES | | External API league identifier (admin-editable) |
| CreatedAtUtc | datetime2 | NO | | When the competition was created |

**Constraints:**
- PK: `Id`
- UNIQUE: `Code` (`UX_Competitions_Code`)

---

### RunningCosts

Admin-recorded website running costs (hosting, fixture API, etc.) used by the recommended-price calculator. Start/end dates are stored so costs can be apportioned/prorated flexibly (ADR 0006). `Frequency` is persisted as an enum-name string.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Name | nvarchar(150) | NO | | Cost name (e.g. "Fasthosts hosting") |
| Amount | decimal(18,2) | NO | | Cost amount (GBP) |
| Frequency | nvarchar(20) | NO | | `Monthly`, `Annual` or `OneOff` |
| StartDateUtc | datetime2 | NO | | When this cost period begins |
| EndDateUtc | datetime2 | YES | | End date (null = ongoing) |
| Notes | nvarchar(500) | YES | | Optional notes |
| CreatedAtUtc | datetime2 | NO | | Creation timestamp |

**Constraints:**
- PK: `Id`

---

### PricingSettings

Single-row, admin-editable global knobs for the recommended-price calculator (ADR 0006): the buffer added on top of costs and the minimum price floor. Provider fees live in `ServiceFees`. Stored so the figures can be tuned without a code deploy. `BufferRate` is a fraction (`0.15` = 15%). Seeded with one row; the calculator falls back to built-in defaults if the row is absent.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| BufferRate | decimal(6,4) | NO | | Buffer added on top of costs (fraction, e.g. 0.15) |
| MinimumFloor | decimal(10,2) | NO | | Smallest price the calculator will suggest (GBP) |

**Constraints:**
- PK: `Id`

---

### ServiceFees

Per-transaction fees charged by third parties (ADR 0006), one row per provider so new providers need no schema change. Stripe takes a percentage + fixed fee on each pass sale; SMS/email providers charge a flat fee per message (`PercentFee` 0). `Provider` is persisted as the enum name. `PercentFee` is a fraction (`0.015` = 1.5%). Seeded with the Stripe row; the calculator falls back to the built-in Stripe default if absent.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Provider | nvarchar(20) | NO | | `Stripe`, `Sms` or `Email` (enum name) |
| PercentFee | decimal(6,4) | NO | | Percentage fee (fraction, e.g. 0.015); 0 for flat-rate providers |
| FixedFee | decimal(10,2) | NO | | Fixed fee per transaction/message (GBP) |

**Constraints:**
- PK: `Id`
- UNIQUE: `Provider`

---

### EmailSettings

Single-row, admin-editable master switch for the app's automated, transactional emails (round digests, reminders, welcome and prize emails). Stored so it can be toggled from the admin UI without a code deploy - chiefly to silence the dev environment when no one is testing. No row is seeded; the app falls back to the built-in default (`EmailsEnabled` = true) when the row is absent, so a fresh or unseeded database keeps sending. The admin email-test tool sends regardless of this switch.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| EmailsEnabled | bit | NO | | Whether automated emails are sent (false suppresses them) |

**Constraints:**
- PK: `Id`

---

### Seasons

Represents a football season within a competition.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| Name | nvarchar(50) | NO | | Season name (e.g., "Premier League 2025/26") |
| IsActive | bit | NO | 1 | Whether season is currently active |
| NumberOfRounds | int | NO | 0 | Total rounds in the season |
| CompetitionId | int | NO | | FK to Competitions (the competition this season belongs to) |
| StartDateUtc | datetime2 | NO | | Season start date |
| EndDateUtc | datetime2 | NO | | Season end date |
| PassStandardPrice | decimal(10,2) | YES | | Admin-set Standard price (> 0) for a pass-required season; NULL for a free season |
| PassPremiumPrice | decimal(10,2) | YES | | Admin-set full price of the Premium tier (>= PassStandardPrice); NULL for a free season |

> Every season requires a Season Pass to take part; "free" seasons simply have no prices, so the pass is acquired for £0 (no payment step). The computed `Season.RequiresPayment` (`PassStandardPrice IS NOT NULL`) only indicates whether acquiring the pass costs money - there is no stored column.

> `ApiLeagueId` and `CompetitionType` previously lived here; both moved to `Competitions` (ADR 0009). The provider id and competition type are now resolved via the season's `Competition` at sync time, and `IsTournament` reads `Competition.Type`.

**Constraints:**
- PK: `Id`
- UNIQUE: `Name`
- FK: `CompetitionId` → `Competitions(Id)` (`FK_Seasons_Competitions`)

---

### SeasonPasses

One record per user per season they take part in. A row is written for **every** participation: a purchase, a free first-season trial, or a £0 `Free` record for free-season play. This is the single source of truth for the Season Pass access gate, and existing participation is backfilled so free play "burns" the free-first-season (ADR 0005).

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers (the participating user) |
| SeasonId | int | NO | | FK to Seasons (the season this pass grants access to) |
| Tier | nvarchar(20) | NO | | Pass tier (enum name): Standard, Premium |
| Source | nvarchar(20) | NO | | How the pass arose (enum name): Purchased, Trial, Free |
| AmountPaid | decimal(10,2) | NO | | Total paid for the pass (0 for trial/free) |
| SmsFeePaid | decimal(10,2) | NO | | SMS uplift actually paid (0 for Standard, trial, or comped) |
| StripePaymentReference | nvarchar(255) | YES | | Stripe payment reference; NULL for trial/free |
| CreatedAtUtc | datetime2 | NO | | When the pass was created |
| SmsSentCount | int | NO | 0 | SMS reminders sent to this user this season |
| RewardRedeemedForSeasonId | int | YES | | Set when this pass's reward funded a later free SMS season |

**Constraints:**
- PK: `Id`
- UNIQUE: `(UserId, SeasonId)` (`UX_SeasonPasses_User_Season`) - one pass per user per season
- FK: `UserId` → `AspNetUsers(Id)` (`FK_SeasonPasses_AspNetUsers`)
- FK: `SeasonId` → `Seasons(Id)` (`FK_SeasonPasses_Seasons`)
- FK: `RewardRedeemedForSeasonId` → `Seasons(Id)` (`FK_SeasonPasses_Seasons_Reward`)

---

### UserOnboardingSkips

Records which onboarding-checklist steps a user has skipped (or had skipped in bulk via "Dismiss"). The steps themselves are defined in code (`OnboardingStepRegistry`) and their completion is derived live from data - this table only stores skips, keyed by the stable string step key. A new step added in code is shown to everyone automatically (no row here, completion derived); nothing about the step set lives in the DB.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| StepKey | nvarchar(100) | NO | | Stable onboarding step key (e.g. `add-mobile`) |
| SkippedAtUtc | datetime2 | NO | | When the step was skipped |

**Constraints:**
- PK: `(UserId, StepKey)`
- FK: `UserId` → `AspNetUsers(Id)` (`FK_UserOnboardingSkips_AspNetUsers`)

---

### UserPayoutDetails

Optional, player-provided bank details for receiving peer-to-peer prize **payouts**. One row per user. The account fields hold **AES-GCM ciphertext** (encrypted via `IFieldEncryptionService`); decrypted only for the player and the admins of prize leagues they're an approved member of. The platform never moves money.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| UserId | nvarchar(450) | NO | | PK, FK to AspNetUsers |
| AccountName | nvarchar(512) | YES | | Payout account name, **AES-GCM ciphertext** |
| SortCode | nvarchar(512) | YES | | Payout sort code, **AES-GCM ciphertext** |
| AccountNumber | nvarchar(512) | YES | | Payout account number, **AES-GCM ciphertext** |
| CreatedAtUtc | datetime2 | NO | | When first saved |
| UpdatedAtUtc | datetime2 | NO | | When last updated |

**Constraints:**
- PK: `UserId`
- FK: `UserId` → `AspNetUsers(Id)` ON DELETE CASCADE (`FK_UserPayoutDetails_AspNetUsers`)

---

### LeaguePayouts

End-of-league settlement tracking: **one aggregated row per (league, winner)** holding the **sum** of that user's `Winnings` for the league and a manual **PaidAtUtc**. Rows are created idempotently once the season is complete (final prize processing done). `Winnings` remains the source of truth - the Round/Monthly/Overall breakdown is computed live from it, never duplicated here.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeagueId | int | NO | | FK to Leagues |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers (the winner) |
| TotalAmount | decimal(18,2) | NO | | Sum of the user's winnings in the league at finalisation |
| PaidAtUtc | datetime2 | YES | | When the admin marked this winner paid (null = outstanding) |
| CreatedAtUtc | datetime2 | NO | | When the payout row was created |
| UpdatedAtUtc | datetime2 | NO | | When the total was last refreshed |

**Constraints:**
- PK: `Id`
- Unique: `(LeagueId, UserId)` (`UQ_LeaguePayouts_League_User`)
- FK: `LeagueId` → `Leagues(Id)` ON DELETE CASCADE (`FK_LeaguePayouts_Leagues`)
- FK: `UserId` → `AspNetUsers(Id)` (`FK_LeaguePayouts_AspNetUsers`)

---

### Rounds

Represents a gameweek within a season.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| SeasonId | int | NO | | FK to Seasons |
| RoundNumber | int | NO | | Round number within season |
| DisplayName | nvarchar(200) | NO | '' | User-facing round name (e.g., "Gameweek 1", "Quarter-finals") |
| Status | nvarchar(50) | NO | 'Draft' | Draft, Published, InProgress, Completed |
| ApiRoundName | nvarchar(128) | YES | | External API round name |
| StartDateUtc | datetime2 | NO | | Round start date |
| DeadlineUtc | datetime2 | NO | | Prediction deadline |
| CompletedDateUtc | datetime2 | YES | | When round was completed |
| LastReminderSentUtc | datetime2 | YES | | Last reminder email sent |
| ResultsDigestSentUtc | datetime2 | YES | | When the round-results digest email was sent (idempotency guard) |
| CompletedDate | datetime2 | YES | | (Legacy column — use CompletedDateUtc instead) |

**Constraints:**
- PK: `Id`
- UNIQUE: `SeasonId, RoundNumber`
- FK: `SeasonId` → `Seasons.Id` (CASCADE DELETE)

---

### TournamentRoundMappings

Admin-configured tournament round structure. Maps tournament stages to prediction rounds.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| SeasonId | int | NO | | FK to Seasons |
| RoundNumber | int | NO | | Prediction round ordering |
| DisplayName | nvarchar(200) | NO | | User-facing round name |
| Stages | nvarchar(500) | NO | | Pipe-delimited TournamentStage values (e.g. `SemiFinals\|ThirdPlace\|Final`) |
| ExpectedMatchCount | int | NO | | Number of matches to create as placeholders |

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
| Status | nvarchar(50) | NO | 'Scheduled' | Scheduled, InProgress, Completed, Postponed |
| ActualHomeTeamScore | int | YES | | Final home score |
| ActualAwayTeamScore | int | YES | | Final away score |
| ExternalId | int | YES | | External API match ID |
| MatchDateTimeUtc | datetime2 | NO | | Kick-off time |
| CustomLockTimeUtc | datetime2 | YES | | Per-match lock time (for tournaments) |
| MatchNumber | int | YES | | Tournament match number (e.g., 1-104 for World Cup). Auto-assigned on creation, editable by admin. |
| PlaceholderHomeName | nvarchar(100) | YES | | e.g., "Winner Match 73" |
| PlaceholderAwayName | nvarchar(100) | YES | | e.g., "Winner Match 75" |
| ApiRoundName | nvarchar(128) | YES | | Original API round name for this match (e.g., "Group Stage - 1") |

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
| Price | decimal(18,2) | NO | 0 | Standard fee |
| IsFree | bit | NO | 0 | Whether league is free to join |
| HasPrizes | bit | NO | 1 | Whether league has prizes |
| PrizeFundOverride | decimal(18,2) | YES | | Admin money added on top of entry fees (additive: pot = Price x ApprovedMembers + this) |
| RequiresMemberApproval | bit | NO | 1 | When set, new joiners are Pending until the admin approves. When clear, joiners are auto-approved. Official free leagues default to clear |
| IsListed | bit | NO | 0 | When set, a private (entry-code) league appears in Available Leagues for discovery; the entry code is still required to join. Ignored for public leagues, which are always listed |
| PointsForExactScore | int | NO | 5 | Points for exact score prediction |
| PointsForCorrectResult | int | NO | 3 | Points for correct result only |
| CreatedAtUtc | datetime2 | NO | GETUTCDATE() | Creation timestamp |
| EntryDeadlineUtc | datetime2 | YES | | Deadline to join league |
| BankAccountName | nvarchar(512) | YES | | Peer-to-peer entry-fee settlement: account name, **AES-GCM ciphertext** |
| BankSortCode | nvarchar(512) | YES | | Peer-to-peer entry-fee settlement: sort code, **AES-GCM ciphertext** |
| BankAccountNumber | nvarchar(512) | YES | | Peer-to-peer entry-fee settlement: account number, **AES-GCM ciphertext** |
| PaymentReferenceTemplate | nvarchar(100) | YES | | Non-sensitive payment-reference hint shown to joiners (plaintext) |

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
| IsArchivedByUser | bit | NO | 0 | When set, the league is hidden from the user's My Leagues carousel by default and surfaced behind the "Show X archived leagues" toggle |
| JoinedAtUtc | datetime2 | NO | GETUTCDATE() | When user requested to join |
| ApprovedAtUtc | datetime2 | YES | | When membership was approved |

**Constraints:**
- PK: `LeagueId, UserId` (composite)
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

---

### LeagueMemberStats

Cached ranking statistics per member per league. Every rank the **My Leagues** tile shows is read from
here by `GetMyLeaguesQueryHandler`; nothing is computed live. `LeagueStatsRepository` is the only writer.

All ranks are relative to the league's **active round** (the round the tile is showing, resolved by the
same priority order the query uses: in progress, then completed within 48 hours, then published). A rank
is `NULL` when it does not exist rather than 0 - the league's season has no active round, the active
round is the first of its season/month/stage so there is no pre-round position to compare against, or the
season has no stage mapping. `NULL` is what suppresses the change arrow on the tile, so it is meaningful.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| LeagueId | int | NO | | FK to Leagues |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| OverallRank | int | YES | | Rank by total boosted points across all rounds |
| MonthRank | int | YES | | Rank by boosted points in the active round's calendar month |
| LiveRoundRank | int | YES | | Rank by boosted points in the active round, moving as matches play |
| SnapshotOverallRank | int | YES | | `OverallRank` as at the start of the active round (rounds before it only) |
| SnapshotMonthRank | int | YES | | `MonthRank` excluding the active round |
| StableRoundRank | int | YES | | Rank by the league's points-per-outcome applied to finished matches of the active round only, so it does not move mid-match |
| StageRank | int | YES | | Rank by boosted points in the active round's tournament stage |
| PreRoundStageRank | int | YES | | `StageRank` excluding the active round |
| ExactScoresRank | int | YES | | Rank by exact-score count across the season. Tournaments show this in the Month slot |
| PreRoundExactScoresRank | int | YES | | `ExactScoresRank` for rounds before the active round |
| LiveRoundPoints | decimal(10,2) | NO | 0.00 | Measure behind `LiveRoundRank`, kept for debugging |
| StableRoundPoints | decimal(10,2) | NO | 0.00 | Measure behind `StableRoundRank`, kept for debugging |

**Constraints:**
- PK: `LeagueId, UserId` (composite)
- FK: `LeagueId` → `Leagues.Id` (**no** cascade - `LeagueRepository.DeleteAsync` removes these rows first)
- FK: `UserId` → `AspNetUsers.Id`

> `SnapshotOverallRank` / `SnapshotMonthRank` keep their original names but are no longer point-in-time
> snapshots. They are recomputed from current results on every refresh, like every other column here.
> See [ADR-0015](../decisions/0015-cache-my-leagues-ranks.md).

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
- UNIQUE: `LeagueId, RoundId, UserId` (index: `UQ_LeagueRoundResults_League_Round_User`)
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `RoundId` → `Rounds.Id`
- FK: `UserId` → `AspNetUsers.Id`

**Indexes:**
- `IX_LeagueRoundResults_League_Round` on `LeagueId, RoundId` (includes UserId, BoostedPoints, BasePoints)
- `IX_LeagueRoundResults_League_User` on `LeagueId, UserId` (includes BoostedPoints, BasePoints)

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
| Stage | nvarchar(50) | YES | | Tournament stage for Section prizes ("Group stage" / "Knockout stage"); null otherwise |

**Constraints:**
- PK: `Id`
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)

**Prize Types:**
- `Overall` - End of season position prizes
- `Monthly` - Monthly aggregate prizes
- `Round` - Weekly round winner prizes
- `MostExactScores` - Most exact predictions prize
- `Section` - Best aggregate per tournament stage (group stage vs knockouts)

> `LeaguePrizeSettings` are the **frozen** post-deadline settlement artefacts. From the
> dynamic-prize-pot feature (ADR-0011) they are produced by freezing a `LeaguePrizeScheme`
> at the entry deadline; the manual `DefinePrizeStructure` path remains as a site-admin override.

---

### LeaguePrizeScheme

The up-front, write-once prize **scheme** an admin configures before entries close (ADR-0011).
One row per league. The concrete prize amounts are derived live by the apportionment engine and
frozen into `LeaguePrizeSettings` at the deadline.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeagueId | int | NO | | FK to Leagues (one scheme per league) |
| SetAtUtc | datetime2 | NO | | When the scheme was set (write-once marker) |
| SetByUserId | nvarchar(450) | NO | | FK to AspNetUsers - who set the scheme |

> Admin top-up money (money the admin puts up on top of the entry fees) is **not** stored here -
> it is the league's existing `Leagues.PrizeFundOverride`, which the dynamic-pot feature treats as
> **additive**: pot = `Price x ApprovedMembers + PrizeFundOverride`.

**Constraints:**
- PK: `Id`
- UNIQUE: `LeagueId` (one scheme per league)
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `SetByUserId` → `AspNetUsers.Id`

---

### LeaguePrizeSchemeEntries

One row per enabled prize category in a scheme: the whole-pound share of each entry that funds it,
and an optional per-league override of the places (rank) table.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeaguePrizeSchemeId | int | NO | | FK to LeaguePrizeScheme |
| Category | nvarchar(20) | NO | | Prize category (Overall, Round, Monthly, MostExactScores, Stages) |
| PerEntryPounds | int | NO | | Whole pounds of each entry allocated to this category |
| RankTableJson | nvarchar(max) | YES | | Optional per-league places-table override (JSON); null uses the product default |

**Constraints:**
- PK: `Id`
- UNIQUE: `(LeaguePrizeSchemeId, Category)`
- FK: `LeaguePrizeSchemeId` → `LeaguePrizeScheme.Id` (CASCADE DELETE)

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

### PrizeNotifications

Append-only sent-log that makes the "Prize Won" email idempotent. `Winnings` rows are deleted and
re-created every time a round is re-processed, so they cannot carry a "notified" flag; this log
persists across re-processing and records that a winner has been emailed about one specific prize.
The send command (`SendPrizeNotificationsCommand`) skips any prize already present here unless forced.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers - the notified winner |
| LeaguePrizeSettingId | int | NO | | FK to LeaguePrizeSettings - the prize won |
| RoundNumber | int | YES | | For Round prizes (matches the won `Winning`) |
| Month | int | YES | | For Monthly prizes, 1-12 (matches the won `Winning`) |
| SentAtUtc | datetime2 | NO | | When the prize-won email was sent |

**Constraints:**
- PK: `Id`
- UNIQUE: `(UserId, LeaguePrizeSettingId, RoundNumber, Month)` - the winning's stable identity; the
  dedup key for idempotency. (SQL Server treats `NULL = NULL` as equal in a unique index, so the
  all-null overall/section prizes are enforced as notified-once.)
- FK: `UserId` → `AspNetUsers.Id`
- FK: `LeaguePrizeSettingId` → `LeaguePrizeSettings.Id`

> Holds no personal data beyond the `UserId` FK (as `Winnings` does), so it is copied verbatim by
> the database refresh tool and needs no anonymisation.

---

### LeagueWelcomeNotifications

Append-only sent-log that makes the league welcome email idempotent. Once a league's entry
deadline has passed (and its prizes are frozen, where a scheme exists), the hourly scheduled task
(`SendLeagueWelcomeEmailsCommand` via `POST /api/external/tasks/send-welcome-emails`) emails every
approved member a one-off welcome covering the member count, prize structure (or leaderboard-only
framing for free leagues) and any enabled boosts. Members already present here are skipped, so the
scan can re-run forever. Only leagues whose deadline passed within the last 7 days are considered,
so historic leagues are never back-filled.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| LeagueId | int | NO | | FK to Leagues |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers - the welcomed member |
| SentAtUtc | datetime2 | NO | | When the welcome email was sent |

**Constraints:**
- PK: `Id`
- UNIQUE: `(LeagueId, UserId)` - the dedup key for idempotency
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `UserId` → `AspNetUsers.Id`

> Holds no personal data beyond the `UserId` FK, so it is copied verbatim by the database refresh
> tool and needs no anonymisation.

---

### PredictionReminderNotifications

Log of ad-hoc "you are missing predictions" reminders sent for a round. An admin (global round
view) or a league owner (their league dashboard) can nudge players who have only partially entered
their predictions; the send is deduped per `(RoundId, UserId)` so a player in several leagues,
nudged by more than one owner, is emailed at most once per throttle window (6 hours) per round.
`LastRemindedUtc` is refreshed on each send, so the row also drives the "reminded N hours ago"
display. Reuses Brevo template 9 (Predictions Missing).

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| RoundId | int | NO | | FK to Rounds |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers - the reminded player |
| LastRemindedUtc | datetime2 | NO | | When the most recent reminder was sent (upserted) |
| RemindedByUserId | nvarchar(450) | NO | | FK to AspNetUsers - who triggered the send (admin or league owner) |

**Constraints:**
- PK: `Id`
- UNIQUE: `(RoundId, UserId)` (`UX_PredictionReminderNotifications_RoundUser`) - the dedup/throttle key
- FK: `RoundId` → `Rounds.Id` (CASCADE DELETE)
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

> Holds no personal data beyond the `UserId`/`RemindedByUserId` FKs, so it is copied verbatim by the
> database refresh tool and needs no anonymisation.

---

### UserBadges

One row per badge a user has earned (Achievements & Badges feature). All badges are global - earned
once, in any league, and counted for that badge regardless of how many leagues the user is in.
`AwardedUtc` holds the real achievement date (backdated when badges are awarded retrospectively).
`LeagueId` is provenance for the caption; `RoundId`/`SeasonId` scope the repeatable badges. Progress
toward the next tier is computed live on read and never stored here - the table only records what has
actually been earned.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers - who earned the badge |
| BadgeKey | nvarchar(50) | NO | | Stable badge key, e.g. `marksman-2`, `sharpshooter-3`, `champion` |
| AwardedUtc | datetime2 | NO | | When it was achieved (backdated for retrospective awards) |
| LeagueId | int | YES | | FK to Leagues - where it happened (provenance for the caption) |
| RoundId | int | YES | | FK to Rounds - scope for per-round badges |
| SeasonId | int | YES | | FK to Seasons - scope for per-season badges |
| Detail | nvarchar(100) | YES | | Caption extra, e.g. the score, streak length or season name |

**Constraints:**
- PK: `Id`
- UNIQUE: `(UserId, BadgeKey, RoundId, SeasonId)` (`UX_UserBadges_UserBadgeRoundSeason`) - the idempotency key. SQL Server treats NULLs as equal in a unique index, so lifetime badges (RoundId + SeasonId both NULL) dedupe to one row ever, per-round badges (RoundId set) to one per round, and per-season badges (SeasonId set) to one per season.
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)
- FK: `LeagueId` → `Leagues.Id` (NO ACTION)
- FK: `RoundId` → `Rounds.Id` (NO ACTION)
- FK: `SeasonId` → `Seasons.Id` (NO ACTION)

> Only `UserId` cascades (GDPR delete of a user removes their badges); the provenance/scope FKs are
> NO ACTION to avoid SQL Server multiple-cascade-path errors. Holds no personal data beyond the
> `UserId` FK, so it is copied verbatim by the database refresh tool and needs no anonymisation.

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

Aggregated results per user per round (across all leagues). League-agnostic, so it holds outcome
counts only - points are per-league and live in `LeagueRoundResults`.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| RoundId | int | NO | | FK to Rounds |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
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
- UNIQUE: `UserId, LeagueId, RoundId, BoostDefinitionId` (prevents duplicate boost applications)
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)
- FK: `LeagueId` → `Leagues.Id` (CASCADE DELETE)
- FK: `SeasonId` → `Seasons.Id`
- FK: `RoundId` → `Rounds.Id`
- FK: `MatchId` → `Matches.Id`
- FK: `BoostDefinitionId` → `BoostDefinitions.Id`

**Indexes:**
- `IX_UserBoostUsages_LeagueRound` on `LeagueId, RoundId`
- `IX_UserBoostUsages_OneBoostPerLeagueRound` on `UserId, LeagueId, RoundId` (unique filtered: WHERE RoundId IS NOT NULL)
- `IX_UserBoostUsages_UserLeagueSeasonBoost` on `UserId, LeagueId, SeasonId, BoostDefinitionId`

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
| **PreferredTheme** | nvarchar(10) | NO | 'light' | **Custom: User's theme preference ('light' or 'dark')** |
| **TermsAcceptedAtUtc** | datetime2 | YES | | **Custom: When the user accepted the Terms of Service, Privacy Policy and 18+ confirmation via the click-wrap wording on Register (GDPR proof of consent)** |
| **MarketingOptInAtUtc** | datetime2 | YES | | **Custom: When the user opted in to marketing emails (NULL if never opted in or has since opted out)** |

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

### PasswordResetTokens

Password reset tokens for email-based password recovery.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Token | nvarchar(128) | NO | | PK - Reset token value |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| CreatedAtUtc | datetime2 | NO | | When token was created |
| ExpiresAtUtc | datetime2 | NO | | When token expires |

**Constraints:**
- PK: `Token`
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

**Indexes:**
- `IX_PasswordResetTokens_ExpiresAtUtc` on `ExpiresAtUtc` (for cleanup queries)
- `IX_PasswordResetTokens_UserId` on `UserId` (for user lookups)

---

### EmailConfirmationTokens

Email verification tokens issued on registration (and resends). Mirrors `PasswordResetTokens`; transient data, skipped by the dev refresh tool.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Token | nvarchar(128) | NO | | PK - confirmation token value |
| UserId | nvarchar(450) | NO | | FK to AspNetUsers |
| CreatedAtUtc | datetime2 | NO | | When token was created |
| ExpiresAtUtc | datetime2 | NO | | When token expires (72h after creation) |

**Constraints:**
- PK: `Token`
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

**Indexes:**
- `IX_EmailConfirmationTokens_UserId` on `UserId` (for user lookups / rate limiting)

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

## Schema Migration Tracking

### SchemaVersions

DbUp's migration journal (ADR-0013). One row per migration script that has been applied **to this
database**. Created and written by `ThePredictions.DatabaseTools` in `Migrate` mode; not used by
the application. Each database (prod / dev / backup) has its **own** journal, so it is
deliberately **excluded** from the `DatabaseRefresher` copy/skip arrays - a data refresh must never
copy or truncate it, or an environment would forget which migrations it has applied.

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | NO | IDENTITY | Primary key |
| ScriptName | nvarchar(255) | NO | | Embedded resource name of the applied migration script |
| Applied | datetime | NO | | When the script was applied |

**Constraints:**
- PK: `Id` (`PK_SchemaVersions_Id`)

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
