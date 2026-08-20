-- Dates every account that existed before [CreatedAtUtc] did (added by 0010).
--
-- ADDITIVE. One UPDATE of existing rows, writing only where the column is still NULL, so nothing that 0010
-- left set can be overwritten. Safe to apply ahead of the code deploy.
--
-- WHY THE BACKFILL IS NOT SIMPLY [TermsAcceptedAtUtc]
--
-- That column is stamped at registration by RecordRegistration, so for anyone who registered after it shipped it
-- *is* the signup time. For everyone who already existed it was backfilled with a flat date at exactly midnight, which is
-- not a time anybody signed up at. On dev that is 23 of 44 accounts sitting on the same 00:00:00 value, and displaying it
-- as "member since ... at 00:00" would present a filler value as a fact.
--
-- So a terms date counts as evidence only when its time is not exactly midnight. A genuine registration at precisely
-- 00:00:00.000 UTC would be excluded by that test - one second in 86,400 - and it would then fall back to the account's
-- own activity, which for a real registration includes the email confirmation token issued seconds later. The failure
-- mode is a slightly better answer rather than a wrong one.
--
-- WHAT COUNTS AS EVIDENCE
--
-- Every per-user row whose date is a creation stamp: the account's own registration consent, the confirmation token
-- issued with it, leagues joined and created, passes bought, predictions made, badges and prizes awarded, payout details
-- saved, boosts played, onboarding steps dismissed, refresh tokens minted at login, password resets requested. The
-- earliest of those is the earliest moment the account demonstrably existed, which is the most that can honestly be said
-- about an account that predates the column.
--
-- System-sent notification tables are deliberately excluded. They record what we sent rather than what the player did,
-- and each implies a membership or a prize that is already counted above, so they would add no coverage.
--
-- Left NULL where there is no evidence at all - an account that registered before the consent columns and has never done
-- anything since. On dev that is 2 accounts of 44. NULL is the honest answer and the screen renders it as unknown; a
-- guess would be indistinguishable from a fact once it is in the column.


WITH [Evidence] AS
(
    SELECT
        u.[Id] AS [UserId],
        u.[TermsAcceptedAtUtc] AS [AtUtc]
    FROM
        [AspNetUsers] u
    WHERE
        u.[TermsAcceptedAtUtc] IS NOT NULL
        AND CAST(u.[TermsAcceptedAtUtc] AS time) <> '00:00:00'

    UNION ALL

    SELECT
        ect.[UserId],
        ect.[CreatedAtUtc]
    FROM
        [EmailConfirmationTokens] ect

    UNION ALL

    SELECT
        lm.[UserId],
        lm.[JoinedAtUtc]
    FROM
        [LeagueMembers] lm

    UNION ALL

    SELECT
        l.[AdministratorUserId],
        l.[CreatedAtUtc]
    FROM
        [Leagues] l

    UNION ALL

    SELECT
        sp.[UserId],
        sp.[CreatedAtUtc]
    FROM
        [SeasonPasses] sp

    UNION ALL

    SELECT
        up.[UserId],
        up.[CreatedAtUtc]
    FROM
        [UserPredictions] up

    UNION ALL

    SELECT
        ub.[UserId],
        ub.[AwardedUtc]
    FROM
        [UserBadges] ub

    UNION ALL

    SELECT
        w.[UserId],
        w.[AwardedDateUtc]
    FROM
        [Winnings] w

    UNION ALL

    SELECT
        lp.[UserId],
        lp.[CreatedAtUtc]
    FROM
        [LeaguePayouts] lp

    UNION ALL

    SELECT
        uos.[UserId],
        uos.[SkippedAtUtc]
    FROM
        [UserOnboardingSkips] uos

    UNION ALL

    SELECT
        upd.[UserId],
        upd.[CreatedAtUtc]
    FROM
        [UserPayoutDetails] upd

    UNION ALL

    SELECT
        ubu.[UserId],
        ubu.[PlayedAtUtc]
    FROM
        [UserBoostUsages] ubu

    UNION ALL

    SELECT
        rt.[UserId],
        rt.[Created]
    FROM
        [RefreshTokens] rt

    UNION ALL

    SELECT
        prt.[UserId],
        prt.[CreatedAtUtc]
    FROM
        [PasswordResetTokens] prt
),
[Earliest] AS
(
    SELECT
        e.[UserId],
        MIN(e.[AtUtc]) AS [FirstKnownAtUtc]
    FROM
        [Evidence] e
    WHERE
        e.[AtUtc] IS NOT NULL
    GROUP BY
        e.[UserId]
)
UPDATE
    u
SET
    u.[CreatedAtUtc] = e.[FirstKnownAtUtc]
FROM
    [AspNetUsers] u
INNER JOIN
    [Earliest] e ON e.[UserId] = u.[Id]
WHERE
    u.[CreatedAtUtc] IS NULL;
