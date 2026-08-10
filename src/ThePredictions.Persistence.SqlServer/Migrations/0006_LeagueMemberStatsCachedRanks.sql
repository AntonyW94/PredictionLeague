-- Additive migration: widen [LeagueMemberStats] so it can cache every rank the My Leagues tile shows.
--
-- The tile's ranks were being computed live on every page view, which cost ~400ms of query-plan
-- compilation per request (the plan was invalidated roughly every minute by the score-update job).
-- The ranks move back onto the write path, so the read becomes a keyed lookup. See
-- docs/decisions/0015-cache-my-leagues-ranks.md.
--
-- Three changes, all widening, so this is SAFE TO APPLY AHEAD OF THE CODE DEPLOY: the currently
-- deployed code writes these columns explicitly and never relies on the DEFAULTs, and it reads them
-- into nullable ints already.
--
-- 1. Four new nullable rank columns for the Group Stage and Exact Scores tiles, which never had a
--    cached equivalent.
-- 2. Drop the DEFAULT ((0)) constraints on the rank columns. A rank of 0 is not a real rank; it is
--    how a missing value used to leak out as a fabricated position. Absent now means NULL.
-- 3. Make the rank columns nullable. A league whose season has no active round genuinely has no
--    rank to show (this is what the live query returned), and a pre-round rank does not exist
--    before the first round of a season, month or stage - that absence is what suppresses the
--    change arrow on the tile.
--
-- [SnapshotOverallRank] / [SnapshotMonthRank] keep their names to keep this migration additive, but
-- they are no longer point-in-time snapshots: they now hold the rank as at the start of the active
-- round, recomputed deterministically from current results. See docs/guides/database-schema.md.

-- 1. New cached ranks -------------------------------------------------------------------------

IF COL_LENGTH('dbo.LeagueMemberStats', 'StageRank') IS NULL
    ALTER TABLE [dbo].[LeagueMemberStats] ADD [StageRank] INT NULL;

IF COL_LENGTH('dbo.LeagueMemberStats', 'PreRoundStageRank') IS NULL
    ALTER TABLE [dbo].[LeagueMemberStats] ADD [PreRoundStageRank] INT NULL;

IF COL_LENGTH('dbo.LeagueMemberStats', 'ExactScoresRank') IS NULL
    ALTER TABLE [dbo].[LeagueMemberStats] ADD [ExactScoresRank] INT NULL;

IF COL_LENGTH('dbo.LeagueMemberStats', 'PreRoundExactScoresRank') IS NULL
    ALTER TABLE [dbo].[LeagueMemberStats] ADD [PreRoundExactScoresRank] INT NULL;
GO

-- 2. Drop the DEFAULT ((0)) constraints on the rank columns ------------------------------------
-- Names are resolved dynamically rather than assumed from the baseline, in case an environment
-- differs. Only the rank columns are touched; LiveRoundPoints/StableRoundPoints keep their
-- DEFAULT ((0.00)) because zero points is a real value.

DECLARE @constraintName SYSNAME;
DECLARE @sql NVARCHAR(MAX);

DECLARE rankDefaults CURSOR LOCAL FAST_FORWARD FOR
    SELECT
        dc.[name]
    FROM
        sys.default_constraints dc
        INNER JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
    WHERE
        dc.[parent_object_id] = OBJECT_ID(N'[dbo].[LeagueMemberStats]')
        AND c.[name] IN (
            N'OverallRank',
            N'MonthRank',
            N'LiveRoundRank',
            N'SnapshotOverallRank',
            N'SnapshotMonthRank',
            N'StableRoundRank'
        );

OPEN rankDefaults;
FETCH NEXT FROM rankDefaults INTO @constraintName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE [dbo].[LeagueMemberStats] DROP CONSTRAINT [' + @constraintName + N'];';
    EXEC sp_executesql @sql;

    FETCH NEXT FROM rankDefaults INTO @constraintName;
END

CLOSE rankDefaults;
DEALLOCATE rankDefaults;
GO

-- 3. Allow the rank columns to be NULL ---------------------------------------------------------

IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[LeagueMemberStats]') AND [name] = N'OverallRank' AND [is_nullable] = 0)
    ALTER TABLE [dbo].[LeagueMemberStats] ALTER COLUMN [OverallRank] INT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[LeagueMemberStats]') AND [name] = N'MonthRank' AND [is_nullable] = 0)
    ALTER TABLE [dbo].[LeagueMemberStats] ALTER COLUMN [MonthRank] INT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[LeagueMemberStats]') AND [name] = N'LiveRoundRank' AND [is_nullable] = 0)
    ALTER TABLE [dbo].[LeagueMemberStats] ALTER COLUMN [LiveRoundRank] INT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[LeagueMemberStats]') AND [name] = N'SnapshotOverallRank' AND [is_nullable] = 0)
    ALTER TABLE [dbo].[LeagueMemberStats] ALTER COLUMN [SnapshotOverallRank] INT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[LeagueMemberStats]') AND [name] = N'SnapshotMonthRank' AND [is_nullable] = 0)
    ALTER TABLE [dbo].[LeagueMemberStats] ALTER COLUMN [SnapshotMonthRank] INT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[LeagueMemberStats]') AND [name] = N'StableRoundRank' AND [is_nullable] = 0)
    ALTER TABLE [dbo].[LeagueMemberStats] ALTER COLUMN [StableRoundRank] INT NULL;
