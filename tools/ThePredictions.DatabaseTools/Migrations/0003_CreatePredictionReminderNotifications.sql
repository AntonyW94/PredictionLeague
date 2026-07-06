-- Additive migration: per-(round, user) log of ad-hoc "you are missing predictions" reminders.
-- Drives the send throttle so a player in multiple leagues, nudged by several league owners,
-- is only emailed once per throttle window per round. Safe to apply ahead of the code deploy.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PredictionReminderNotifications]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[PredictionReminderNotifications](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RoundId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[LastRemindedUtc] [datetime2](7) NOT NULL,
	[RemindedByUserId] [nvarchar](450) NOT NULL,
 CONSTRAINT [PK_PredictionReminderNotifications] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[PredictionReminderNotifications] WITH CHECK ADD CONSTRAINT [FK_PredictionReminderNotifications_Rounds] FOREIGN KEY([RoundId])
REFERENCES [dbo].[Rounds] ([Id])
ON DELETE CASCADE

ALTER TABLE [dbo].[PredictionReminderNotifications] WITH CHECK ADD CONSTRAINT [FK_PredictionReminderNotifications_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE

CREATE UNIQUE NONCLUSTERED INDEX [UX_PredictionReminderNotifications_RoundUser] ON [dbo].[PredictionReminderNotifications]
(
	[RoundId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
END
