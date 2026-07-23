-- Additive migration: one row per badge a user has earned (Achievements & Badges feature).
-- All badges are global (earned once, in any league). Idempotency and repeat-scope are enforced
-- by a single unique index on (UserId, BadgeKey, RoundId, SeasonId) - SQL Server treats NULLs as
-- equal in a unique index, so lifetime (both NULL), per-round (RoundId set) and per-season
-- (SeasonId set) badges all deduplicate correctly. Only UserId cascades (GDPR delete); the
-- provenance/scope FKs are NO ACTION to avoid multiple-cascade-path errors. Safe to apply ahead
-- of the code deploy.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserBadges]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[UserBadges](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[BadgeKey] [nvarchar](50) NOT NULL,
	[AwardedUtc] [datetime2](7) NOT NULL,
	[LeagueId] [int] NULL,
	[RoundId] [int] NULL,
	[SeasonId] [int] NULL,
	[Detail] [nvarchar](100) NULL,
 CONSTRAINT [PK_UserBadges] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[UserBadges] WITH CHECK ADD CONSTRAINT [FK_UserBadges_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE

ALTER TABLE [dbo].[UserBadges] WITH CHECK ADD CONSTRAINT [FK_UserBadges_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])

ALTER TABLE [dbo].[UserBadges] WITH CHECK ADD CONSTRAINT [FK_UserBadges_Rounds] FOREIGN KEY([RoundId])
REFERENCES [dbo].[Rounds] ([Id])

ALTER TABLE [dbo].[UserBadges] WITH CHECK ADD CONSTRAINT [FK_UserBadges_Seasons] FOREIGN KEY([SeasonId])
REFERENCES [dbo].[Seasons] ([Id])

CREATE UNIQUE NONCLUSTERED INDEX [UX_UserBadges_UserBadgeRoundSeason] ON [dbo].[UserBadges]
(
	[UserId] ASC,
	[BadgeKey] ASC,
	[RoundId] ASC,
	[SeasonId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
END
