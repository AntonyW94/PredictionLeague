# Task: Database & Schema

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the `Seasons.RequiresPass` column and the `SeasonPasses` table, and keep schema docs + DatabaseTools in sync (mandatory per CLAUDE.md).

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| DB migration / script | Create | `Seasons.RequiresPass` + `SeasonPasses` table |
| `docs/guides/database-schema.md` | Modify | Document new column + table (single source of truth) |
| `tools/ThePredictions.DatabaseTools/DatabaseRefresher.cs` | Modify | Add `SeasonPasses` to `TableCopyOrder` (after `Seasons`/users) |
| `tools/ThePredictions.DatabaseTools/DataAnonymiser.cs` | Modify | Anonymise `SeasonPasses` (holds `UserId` + payment ref) |
| `tools/ThePredictions.DatabaseTools/PersonalDataVerifier.cs` | Modify | Verify `StripePaymentReference` handled |

## Implementation Steps

### Step 1: Schema changes

```sql
ALTER TABLE [Seasons] ADD [RequiresPass] BIT NOT NULL DEFAULT (0);

CREATE TABLE [SeasonPasses] (
    [Id]                     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId]                 INT NOT NULL,
    [SeasonId]               INT NOT NULL,
    [Tier]                   INT NOT NULL,        -- 0 Entry, 1 EntryPlusSms
    [Source]                 INT NOT NULL,        -- 0 Purchased, 1 Trial
    [AmountPaid]             DECIMAL(10,2) NOT NULL,
    [StripePaymentReference] NVARCHAR(255) NULL,
    [CreatedAtUtc]           DATETIME2 NOT NULL,
    [SmsSentCount]           INT NOT NULL DEFAULT (0),    -- SMS reminders sent this season (powers early-bird reward)
    CONSTRAINT [FK_SeasonPasses_Seasons] FOREIGN KEY ([SeasonId]) REFERENCES [Seasons]([Id]),
    CONSTRAINT [FK_SeasonPasses_Users]   FOREIGN KEY ([UserId])   REFERENCES [AspNetUsers]([Id])
);
CREATE UNIQUE INDEX [UX_SeasonPasses_User_Season] ON [SeasonPasses]([UserId], [SeasonId]);
```

- Unique index enforces **one pass per user per season**.
- `DEFAULT (0)` on `RequiresPass` grandfathers every existing season as free.

### Step 2: Update schema docs

- Add `RequiresPass` to the Seasons table section and a full `SeasonPasses` section in `docs/guides/database-schema.md`.

### Step 3: Update DatabaseTools

- `TableCopyOrder`: insert `SeasonPasses` after `Seasons` and the users table (respect FK order).
- `DataAnonymiser`: scrub/neutralise `StripePaymentReference` and any user linkage as per existing rules for personal data.
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
