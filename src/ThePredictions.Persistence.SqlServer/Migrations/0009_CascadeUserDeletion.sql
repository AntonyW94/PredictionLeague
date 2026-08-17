-- Deleting a user account failed with a bare 500.
--
-- Admin "Delete user" ends in a single-table DELETE FROM [AspNetUsers] (DapperUserStore.DeleteAsync). Twenty-four foreign
-- keys point at that table; thirteen already cascaded, eleven did not. Any row in one of the eleven made the DELETE fail
-- with error 547, and because nothing catches SqlException the caller got ErrorHandlingMiddleware's unhandled bucket:
-- "An internal server error has occurred." In practice that meant almost anyone who had ever bought a season pass or
-- played a round could not be deleted, and the screen never said why.
--
-- Nine of the eleven are the user's own records, and they cascade here. The two that remain deliberately do not:
--
--   [Leagues].[AdministratorUserId]        - cascading would delete the league itself, and with it every other member's
--                                            membership, results and prizes. DeleteUserCommandHandler already requires a
--                                            replacement administrator to be chosen, which is the correct answer.
--   [LeaguePrizeScheme].[SetByUserId]      - the scheme belongs to the league, not to whoever last configured it.
--                                            Cascading would strip a league of its prize configuration because an
--                                            unrelated account was closed.
--
-- Both are other members' data rather than the deleted user's, so both keep NO ACTION and both remain capable of blocking
-- a delete - the league one via a message the admin can act on, which is existing behaviour.
--
-- No multiple-cascade-path risk (error 1785): every table below reaches [AspNetUsers] by exactly one route. The alternative
-- routes run through [Leagues], [Rounds], [Seasons] and [LeaguePrizeSettings], none of which is itself reachable from
-- [AspNetUsers] by a cascading key - precisely because [Leagues].[AdministratorUserId] is left alone above.
--
-- Destructive: deleting an account now destroys its season pass purchase and payout history rather than refusing. That is
-- the intended behaviour, not a side effect. Forward-only - reversing it means a new script restoring NO ACTION.

ALTER TABLE [SeasonPasses] DROP CONSTRAINT [FK_SeasonPasses_AspNetUsers];
ALTER TABLE [SeasonPasses] ADD CONSTRAINT [FK_SeasonPasses_AspNetUsers]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [UserOnboardingSkips] DROP CONSTRAINT [FK_UserOnboardingSkips_AspNetUsers];
ALTER TABLE [UserOnboardingSkips] ADD CONSTRAINT [FK_UserOnboardingSkips_AspNetUsers]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [Winnings] DROP CONSTRAINT [FK_Winnings_AspNetUsers];
ALTER TABLE [Winnings] ADD CONSTRAINT [FK_Winnings_AspNetUsers]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [LeaguePayouts] DROP CONSTRAINT [FK_LeaguePayouts_AspNetUsers];
ALTER TABLE [LeaguePayouts] ADD CONSTRAINT [FK_LeaguePayouts_AspNetUsers]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [LeagueMemberStats] DROP CONSTRAINT [FK_LeagueMemberStats_User];
ALTER TABLE [LeagueMemberStats] ADD CONSTRAINT [FK_LeagueMemberStats_User]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [LeagueRoundResults] DROP CONSTRAINT [FK_LeagueRoundResults_Users];
ALTER TABLE [LeagueRoundResults] ADD CONSTRAINT [FK_LeagueRoundResults_Users]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [RoundResults] DROP CONSTRAINT [FK_RoundResults_Users];
ALTER TABLE [RoundResults] ADD CONSTRAINT [FK_RoundResults_Users]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [LeagueWelcomeNotifications] DROP CONSTRAINT [FK_LeagueWelcomeNotifications_AspNetUsers];
ALTER TABLE [LeagueWelcomeNotifications] ADD CONSTRAINT [FK_LeagueWelcomeNotifications_AspNetUsers]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [PrizeNotifications] DROP CONSTRAINT [FK_PrizeNotifications_AspNetUsers_UserId];
ALTER TABLE [PrizeNotifications] ADD CONSTRAINT [FK_PrizeNotifications_AspNetUsers_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
