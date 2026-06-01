# Task: Database & Schema

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ✅ Phase A — buildable now (no accounts).

## Status

**Not Started** | In Progress | Complete

## Goal

Add the `Seasons` price columns (`PassStandardPrice`/`PassPremiumPrice` - there is no stored `RequiresPayment`; it is derived from price presence), the `SeasonPasses` table, and the `RunningCosts` table (Task 14), and keep schema docs + DatabaseTools in sync (mandatory per CLAUDE.md). Note: the `Seasons.PassStandardPrice`/`PassPremiumPrice` columns were already added in the Task 06 stage; this task adds the remaining tables.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| DB migration / script | Create | `Competitions` table; `Seasons` columns (+ `CompetitionId`, drop `ApiLeagueId`); `SeasonPasses` + `RunningCosts` tables; **encrypted `Leagues` bank columns + `UserPayoutDetails` table + `LeaguePayouts` table** (ADR 0010) |
| `docs/guides/database-schema.md` | Modify | Document new columns + tables; reflect `ApiLeagueId` moving from `Seasons` to `Competitions`; note encrypted bank columns |
| `tools/ThePredictions.DatabaseTools/DatabaseRefresher.cs` | Modify | Add `Competitions` (before `Seasons`), `SeasonPasses`, `RunningCosts`, `UserPayoutDetails`, `LeaguePayouts` to `TableCopyOrder` (respect FK order) |
| `tools/ThePredictions.DatabaseTools/DataAnonymiser.cs` | Modify | Anonymise `SeasonPasses` (UserId + payment ref) **and all encrypted bank details (`Leagues` + `UserPayoutDetails`)** |
| `tools/ThePredictions.DatabaseTools/PersonalDataVerifier.cs` | Modify | Verify `StripePaymentReference` **and bank details** never survive a refresh |

## Implementation Steps

### Step 1: Schema changes

```sql
-- Competitions reference table (ADR 0009) — created first so Seasons can FK to it
CREATE TABLE [Competitions] (
    [Id]            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Code]          NVARCHAR(50) NOT NULL,        -- stable slug, e.g. 'WORLD_CUP', 'EPL'
    [Name]          NVARCHAR(200) NOT NULL,
    [Type]          INT NOT NULL,                 -- League / Tournament (moved from Seasons.CompetitionType)
    [LogoAssetPath] NVARCHAR(500) NULL,           -- hosted asset (uploaded via admin), served from our domain
    [ApiLeagueId]   INT NULL,                     -- provider's league id; ADMIN-EDITABLE, no deploy
    [CreatedAtUtc]  DATETIME2 NOT NULL
);
CREATE UNIQUE INDEX [UX_Competitions_Code] ON [Competitions]([Code]);

-- NOTE: [PassStandardPrice]/[PassPremiumPrice]/[CompetitionId] were already added in earlier stages
-- (CompetitionId in the Competitions refactor; prices in the Season pass domain stage).
-- There is no [RequiresPayment] column - a season is pass-required when [PassStandardPrice] IS NOT NULL.
ALTER TABLE [Seasons] ADD
    [PassStandardPrice]    DECIMAL(10,2) NULL,           -- admin-set entry price (> 0) for a paid season; NULL = free
    [PassPremiumPrice]      DECIMAL(10,2) NULL,           -- admin-set full price of the +SMS tier (>= PassStandardPrice); NULL = free
    [CompetitionId] INT NULL;                     -- FK to Competitions (ADR 0009); backfill then enforce NOT NULL
-- after backfill: ADD CONSTRAINT [FK_Seasons_Competitions] FOREIGN KEY ([CompetitionId]) REFERENCES [Competitions]([Id]);
-- after backfill: ALTER TABLE [Seasons] DROP COLUMN [ApiLeagueId];      -- provider id now lives on Competitions
-- after backfill: ALTER TABLE [Seasons] DROP COLUMN [CompetitionType];  -- type now lives on Competitions.Type

CREATE TABLE [SeasonPasses] (
    [Id]                       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId]                   NVARCHAR(450) NOT NULL,   -- FK to AspNetUsers (string identity id)
    [SeasonId]                 INT NOT NULL,
    [Tier]                     NVARCHAR(20) NOT NULL,        -- Standard | Premium (enum stored as string, as elsewhere)
    [Source]                   NVARCHAR(20) NOT NULL,        -- Purchased | Trial | Free
    [AmountPaid]               DECIMAL(10,2) NOT NULL,
    [SmsFeePaid]               DECIMAL(10,2) NOT NULL DEFAULT (0),  -- SMS uplift actually paid (0 if comped/trial/standard)
    [StripePaymentReference]   NVARCHAR(255) NULL,
    [CreatedAtUtc]             DATETIME2 NOT NULL,
    [SmsSentCount]             INT NOT NULL DEFAULT (0),    -- SMS reminders sent this season (powers reward)
    [RewardRedeemedForSeasonId] INT NULL,                  -- set when this pass's leftover funded a later free SMS season
    -- NOTE: SMS pause is a per-user notification preference (Task 11), NOT a per-pass column.
    [RefundedAtUtc]            DATETIME2 NULL,              -- set when the pass is refunded (pre-season-start; ADR 0005, Task 17)
    CONSTRAINT [FK_SeasonPasses_Seasons] FOREIGN KEY ([SeasonId]) REFERENCES [Seasons]([Id]),
    CONSTRAINT [FK_SeasonPasses_Users]   FOREIGN KEY ([UserId])   REFERENCES [AspNetUsers]([Id])
);
CREATE UNIQUE INDEX [UX_SeasonPasses_User_Season] ON [SeasonPasses]([UserId], [SeasonId]);

CREATE TABLE [RunningCosts] (
    [Id]            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name]          NVARCHAR(200) NOT NULL,
    [Amount]        DECIMAL(10,2) NOT NULL,        -- the price
    [Frequency]     INT NOT NULL,                 -- cost type: 0 Monthly, 1 Annual, 2 OneOff
    [StartDateUtc]  DATETIME2 NOT NULL,           -- when this cost (period) begins
    [EndDateUtc]    DATETIME2 NULL,               -- end date (null = ongoing)
    [Notes]         NVARCHAR(500) NULL,
    [CreatedAtUtc]  DATETIME2 NOT NULL
);

-- League admin's RECEIVING details (ADR 0010) — stored ENCRYPTED (app-level AES, key in Key Vault)
ALTER TABLE [Leagues] ADD
    [BankAccountNameEnc]       NVARCHAR(512) NULL,   -- ciphertext
    [BankSortCodeEnc]          NVARCHAR(512) NULL,   -- ciphertext
    [BankAccountNumberEnc]     NVARCHAR(512) NULL,   -- ciphertext
    [PaymentReferenceTemplate] NVARCHAR(100) NULL;   -- not sensitive (e.g. "{PlayerName}")

-- Player PAYOUT details (ADR 0010) — optional, ENCRYPTED; visible only to admins of leagues the user is in
CREATE TABLE [UserPayoutDetails] (
    [UserId]           INT NOT NULL PRIMARY KEY,
    [AccountNameEnc]   NVARCHAR(512) NOT NULL,       -- ciphertext
    [SortCodeEnc]      NVARCHAR(512) NOT NULL,       -- ciphertext
    [AccountNumberEnc] NVARCHAR(512) NOT NULL,       -- ciphertext
    [UpdatedAtUtc]     DATETIME2 NOT NULL,
    CONSTRAINT [FK_UserPayoutDetails_Users] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id])
);

-- Aggregated payout per (league, user) (ADR 0010) — admin marks ONE total paid, not each individual winning
CREATE TABLE [LeaguePayouts] (
    [Id]           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [LeagueId]     INT NOT NULL,
    [UserId]       INT NOT NULL,
    [TotalAmount]  DECIMAL(10,2) NOT NULL,   -- sum of this user's Winnings in the league
    [PaidAtUtc]    DATETIME2 NULL,           -- null = outstanding
    [UpdatedAtUtc] DATETIME2 NOT NULL,
    CONSTRAINT [FK_LeaguePayouts_Leagues] FOREIGN KEY ([LeagueId]) REFERENCES [Leagues]([Id]),
    CONSTRAINT [FK_LeaguePayouts_Users]   FOREIGN KEY ([UserId])   REFERENCES [AspNetUsers]([Id])
);
CREATE UNIQUE INDEX [UX_LeaguePayouts_League_User] ON [LeaguePayouts]([LeagueId], [UserId]);
```

- Unique index enforces **one pass per user per season** — and because rows are per-(user, season), a user can hold **multiple concurrent passes for overlapping seasons** (e.g. World Cup + Premier League) with no extra modelling.
- **Every participation has a record:** free seasons store a **£0 `Free`** pass; paid seasons store `Purchased`/`Trial`. This is what makes free play **burn the free-first-season** (ADR 0005).
- **Backfill (ADR 0005):** for every existing approved `(user, season)` membership, insert a **£0 `Free`** `SeasonPass` (Source 2), so existing players already have records and pay for their first paid season. One-time migration query.
- Existing seasons have NULL `PassStandardPrice`/`PassPremiumPrice`, so they are all grandfathered as free (a season is paid only once priced).
- **Competition migration (ADR 0009):** insert a `Competitions` row per distinct existing `Season.ApiLeagueId` (set `Code`/`Name`/`ApiLeagueId`, and `Type` from the seasons' existing `CompetitionType`; logo added later), add `Seasons.CompetitionId` nullable, **backfill** it, add the FK, make it `NOT NULL`, then **drop `Seasons.ApiLeagueId` and `Seasons.CompetitionType`**. Update the existing season-sync handler and any `Season.ApiLeagueId` / `Season.CompetitionType` / `Season.IsTournament` readers to go via the season's `Competition` — see Task 16.
- `RunningCosts` has **no personal data** — copy as-is in the refresh (no anonymisation), but include it in `TableCopyOrder`.
- **Encrypted bank details (ADR 0010):** `Leagues` (admin receiving details) and `UserPayoutDetails` (player payout details) hold **ciphertext only** (app-level AES, key in Key Vault — never plaintext in the DB). `DataAnonymiser` must replace them with dummy values on dev refresh and `PersonalDataVerifier` must assert no real values survive. **`LeaguePayouts`** holds one **aggregated total per (league, user)** with `PaidAtUtc` — the admin marks the **single total** paid, not each individual `Winnings` row (no personal data beyond `UserId`/amount, so copied as-is). The Round/Monthly/Overall **breakdown is NOT stored here** — it stays **computed live from `Winnings`** (the source of truth) for the dashboard and payouts list. **Mark-as-paid is only enabled once the season is complete** (all rounds done). Build/UX in **Tasks 19 & 20**.

### Step 2: Update schema docs

- Add `PassStandardPrice`/`PassPremiumPrice` to the Seasons section (no `RequiresPayment` column - note it is derived), and full `SeasonPasses` and `RunningCosts` sections in `docs/guides/database-schema.md`.

### Step 3: Update DatabaseTools

- `TableCopyOrder`: insert `SeasonPasses` after `Seasons` and the users table; insert `RunningCosts` (no FK dependencies) anywhere appropriate.
- `DataAnonymiser`: scrub/neutralise `SeasonPasses.StripePaymentReference` and any user linkage per existing personal-data rules. `RunningCosts` needs no anonymisation.
- `PersonalDataVerifier`: assert `StripePaymentReference` is anonymised in refreshed dev data.

## Verification

- [ ] Migration applies cleanly to dev DB.
- [ ] `database-schema.md` updated.
- [ ] DatabaseTools refresh runs; `SeasonPasses` copied in correct FK order; payment reference anonymised; verifier passes.
- [ ] Match real `AspNetUsers` PK type/name when writing the FK.

## Edge Cases to Consider

- Confirm the users table/PK name (`AspNetUsers`.`Id`) and its type before writing the FK.
- Decimal precision matches `Leagues.Price` convention.

## Notes

Brackets + PascalCase + aliases per SQL conventions. Keep enum-to-int mapping documented in schema doc.
