-- Additive migration: single-row, admin-editable master switch for automated emails.
-- Safe to apply ahead of the code deploy. No row is seeded; absence means "emails on" (production default).
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EmailSettings]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[EmailSettings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[EmailsEnabled] [bit] NOT NULL,
 CONSTRAINT [PK_EmailSettings] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
