-- Destructive migration: drop the vestigial [RoundResults].[TotalPoints] column.
--
-- RoundResults is the global, league-agnostic per-user-per-round row (outcome counts). Points are a
-- per-league setting and live in LeagueRoundResults (BasePoints/BoostedPoints), so a single global
-- TotalPoints here is meaningless. The maintaining MERGE (RoundRepository.UpdateRoundResultsAsync)
-- only ever writes the three counts, so the column sits at its DEFAULT of 0, and nothing reads it
-- (every leaderboard/record/recap/dashboard query reads points from LeagueRoundResults). It already
-- caused one bug (the "On the Board" badge rule). See docs/todo history for the full rationale.
--
-- Forward-only and safe to apply with or after the deploy (no code references the column). The
-- column has a DEFAULT constraint (DF_RoundResults_TotalPoints), which must be dropped before the
-- column can be. Both steps are guarded so the script is a no-op if already applied. The constraint
-- name is resolved dynamically in case it differs from the baseline name on any environment.

DECLARE @defaultConstraint sysname;

SELECT
    @defaultConstraint = dc.[name]
FROM
    sys.default_constraints dc
    INNER JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
WHERE
    dc.[parent_object_id] = OBJECT_ID(N'[dbo].[RoundResults]')
    AND c.[name] = N'TotalPoints';

IF @defaultConstraint IS NOT NULL
    EXEC('ALTER TABLE [dbo].[RoundResults] DROP CONSTRAINT [' + @defaultConstraint + '];');

IF COL_LENGTH('dbo.RoundResults', 'TotalPoints') IS NOT NULL
    ALTER TABLE [dbo].[RoundResults] DROP COLUMN [TotalPoints];
