# Task: Database & Schema

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the `Seasons.RequiresPass` + price columns, the `SeasonPasses` table, and the `RunningCosts` table (Task 14), and keep schema docs + DatabaseTools in sync (mandatory per CLAUDE.md).

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| DB migration / script | Create | `Seasons` columns + `SeasonPasses` + `RunningCosts` tables |
| `docs/guides/database-schema.md` | Modify | Document new columns + tables (single source of truth) |
| `tools/ThePredictions.DatabaseTools/DatabaseRefresher.cs` | Modify | Add `SeasonPasses` + `RunningCosts` to `TableCopyOrder` (respect FK order) |
| `tools/ThePredictions.DatabaseTools/DataAnonymiser.cs` | Modify | Anonymise `SeasonPasses` (holds `UserId` + payment ref) |
| `tools/ThePredictions.DatabaseTools/PersonalDataVerifier.cs` | Modify | Verify `StripePaymentReference` handled |

## Implementation Steps

### Step 1: Schema changes

```sql
ALTER TABLE [Seasons] ADD
    [RequiresPass] BIT NOT NULL DEFAULT (0),
    [EntryPrice]   DECIMAL(10,2) NULL,            -- admin-set; required when RequiresPass = 1
    [SmsPrice]     DECIMAL(10,2) NULL;            -- admin-set full price of the +SMS tier

CREATE TABLE [SeasonPasses] (
    [Id]                       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId]                   INT NOT NULL,
    [SeasonId]                 INT NOT NULL,
    [Tier]                     INT NOT NULL,        -- 0 Entry, 1 EntryPlusSms
    [Source]                   INT NOT NULL,        -- 0 Purchased, 1 Trial (2 RewardUpgrade if added)
    [AmountPaid]               DECIMAL(10,2) NOT NULL,
    [SmsFeePaid]               DECIMAL(10,2) NOT NULL DEFAULT (0),  -- SMS uplift actually paid (0 if comped/trial/entry)
    [StripePaymentReference]   NVARCHAR(255) NULL,
    [CreatedAtUtc]             DATETIME2 NOT NULL,
    [SmsSentCount]             INT NOT NULL DEFAULT (0),    -- SMS reminders sent this season (powers reward)
    [RewardRedeemedForSeasonId] INT NULL,                  -- set when this pass's leftover funded a later free SMS season
    [SmsPaused]                BIT NOT NULL DEFAULT (0),    -- user paused their SMS for this season (in-app toggle)
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
    [EndDateUtc]    DATETIME2 NULL,               -- end / next renewal (null = ongoing)
    [Payer]         INT NOT NULL,                 -- 0 Business, 1 PersonalUntilRenewal
    [Notes]         NVARCHAR(500) NULL,
    [CreatedAtUtc]  DATETIME2 NOT NULL
);
```

- Unique index enforces **one pass per user per season**.
- `DEFAULT (0)` on `RequiresPass` grandfathers every existing season as free; prices stay NULL on those.
- `RunningCosts` has **no personal data** — copy as-is in the refresh (no anonymisation), but include it in `TableCopyOrder`.

### Step 2: Update schema docs

- Add `RequiresPass`/`EntryPrice`/`SmsPrice` to the Seasons section, and full `SeasonPasses` and `RunningCosts` sections in `docs/guides/database-schema.md`.

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
