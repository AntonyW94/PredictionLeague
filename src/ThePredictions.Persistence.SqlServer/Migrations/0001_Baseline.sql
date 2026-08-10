/*
    0001_Baseline.sql
    Baseline schema for ThePredictions. Generated from the LIVE production database
    via SMO and made fully idempotent (every object guarded with IF [NOT] EXISTS).

      * On the existing prod / dev / backup databases every guard is already satisfied,
        so this script performs NO DDL - DbUp simply records it as applied in SchemaVersions.
      * On a brand-new empty database it builds the entire schema from zero, in
        FK-dependency order.

    Collation: COLLATE clauses are intentionally omitted; a new database inherits its
    default collation, which MUST be SQL_Latin1_General_CP1_CI_AS to match production
    (see ADR-0013).

    DO NOT EDIT applied migrations. To change the schema, add a new NNNN_*.sql script.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Teams]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Teams](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[LogoUrl] [nvarchar](255) NULL,
	[Abbreviation] [nvarchar](3) NOT NULL,
	[ShortName] [nvarchar](16) NOT NULL,
	[ApiTeamId] [int] NULL,
 CONSTRAINT [PK_Teams] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Teams_Name] UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ServiceFees]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ServiceFees](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Provider] [nvarchar](20) NOT NULL,
	[PercentFee] [decimal](6, 4) NOT NULL,
	[FixedFee] [decimal](10, 2) NOT NULL,
 CONSTRAINT [PK_ServiceFees] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ServiceFees_Provider] UNIQUE NONCLUSTERED 
(
	[Provider] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RunningCosts]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[RunningCosts](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](150) NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[Frequency] [nvarchar](20) NOT NULL,
	[StartDateUtc] [datetime2](7) NOT NULL,
	[EndDateUtc] [datetime2](7) NULL,
	[Notes] [nvarchar](500) NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_RunningCosts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PricingSettings]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[PricingSettings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[BufferRate] [decimal](6, 4) NOT NULL,
	[MinimumFloor] [decimal](10, 2) NOT NULL,
 CONSTRAINT [PK_PricingSettings] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetRoles]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AspNetRoles](
	[Id] [nvarchar](450) NOT NULL,
	[Name] [nvarchar](256) NULL,
	[NormalizedName] [nvarchar](256) NULL,
	[ConcurrencyStamp] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Competitions]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Competitions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Type] [int] NOT NULL,
	[LogoUrl] [nvarchar](500) NULL,
	[ApiLeagueId] [int] NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[Description] [nvarchar](max) NULL,
 CONSTRAINT [PK_Competitions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BoostDefinitions]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[BoostDefinitions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](255) NULL,
	[Scope] [nvarchar](20) NOT NULL,
	[ImageUrl] [nvarchar](255) NULL,
	[SelectedImageUrl] [nvarchar](255) NULL,
	[DisabledImageUrl] [nvarchar](255) NULL,
	[Tooltip] [nvarchar](255) NULL,
 CONSTRAINT [PK_BoostDefinitions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_BoostDefinitions_Code] UNIQUE NONCLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AspNetUsers](
	[Id] [nvarchar](450) NOT NULL,
	[UserName] [nvarchar](256) NULL,
	[NormalizedUserName] [nvarchar](256) NULL,
	[Email] [nvarchar](256) NULL,
	[NormalizedEmail] [nvarchar](256) NULL,
	[EmailConfirmed] [bit] NOT NULL,
	[PasswordHash] [nvarchar](max) NULL,
	[SecurityStamp] [nvarchar](max) NULL,
	[ConcurrencyStamp] [nvarchar](max) NULL,
	[PhoneNumber] [nvarchar](max) NULL,
	[PhoneNumberConfirmed] [bit] NOT NULL,
	[TwoFactorEnabled] [bit] NOT NULL,
	[LockoutEnd] [datetimeoffset](7) NULL,
	[LockoutEnabled] [bit] NOT NULL,
	[AccessFailedCount] [int] NOT NULL,
	[FirstName] [nvarchar](100) NOT NULL,
	[LastName] [nvarchar](100) NOT NULL,
	[PreferredTheme] [nvarchar](10) NOT NULL,
	[TermsAcceptedAtUtc] [datetime2](7) NULL,
	[MarketingOptInAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserRoles]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AspNetUserRoles](
	[UserId] [nvarchar](450) NOT NULL,
	[RoleId] [nvarchar](450) NOT NULL,
 CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserLogins]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AspNetUserLogins](
	[LoginProvider] [nvarchar](450) NOT NULL,
	[ProviderKey] [nvarchar](450) NOT NULL,
	[ProviderDisplayName] [nvarchar](max) NULL,
	[UserId] [nvarchar](450) NOT NULL,
 CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED 
(
	[LoginProvider] ASC,
	[ProviderKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserClaims]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AspNetUserClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[ClaimType] [nvarchar](max) NULL,
	[ClaimValue] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserTokens]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AspNetUserTokens](
	[UserId] [nvarchar](450) NOT NULL,
	[LoginProvider] [nvarchar](450) NOT NULL,
	[Name] [nvarchar](450) NOT NULL,
	[Value] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[LoginProvider] ASC,
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetRoleClaims]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AspNetRoleClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RoleId] [nvarchar](450) NOT NULL,
	[ClaimType] [nvarchar](max) NULL,
	[ClaimValue] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PasswordResetTokens]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[PasswordResetTokens](
	[Token] [nvarchar](128) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[ExpiresAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY CLUSTERED 
(
	[Token] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RefreshTokens]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[RefreshTokens](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[Token] [nvarchar](max) NOT NULL,
	[Expires] [datetime2](7) NOT NULL,
	[Created] [datetime2](7) NOT NULL,
	[Revoked] [datetime2](7) NULL,
 CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserPayoutDetails]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[UserPayoutDetails](
	[UserId] [nvarchar](450) NOT NULL,
	[AccountName] [nvarchar](512) NULL,
	[SortCode] [nvarchar](512) NULL,
	[AccountNumber] [nvarchar](512) NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_UserPayoutDetails] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserOnboardingSkips]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[UserOnboardingSkips](
	[UserId] [nvarchar](450) NOT NULL,
	[StepKey] [nvarchar](100) NOT NULL,
	[SkippedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_UserOnboardingSkips] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[StepKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EmailConfirmationTokens]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[EmailConfirmationTokens](
	[Token] [nvarchar](128) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[ExpiresAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_EmailConfirmationTokens] PRIMARY KEY CLUSTERED 
(
	[Token] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Seasons]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Seasons](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[NumberOfRounds] [int] NOT NULL,
	[StartDateUtc] [datetime2](7) NOT NULL,
	[EndDateUtc] [datetime2](7) NOT NULL,
	[CompetitionId] [int] NOT NULL,
	[PassStandardPrice] [decimal](10, 2) NULL,
	[PassPremiumPrice] [decimal](10, 2) NULL,
 CONSTRAINT [PK_Seasons] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Seasons_Name] UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SeasonPasses]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[SeasonPasses](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[SeasonId] [int] NOT NULL,
	[Tier] [nvarchar](20) NOT NULL,
	[Source] [nvarchar](20) NOT NULL,
	[AmountPaid] [decimal](10, 2) NOT NULL,
	[SmsFeePaid] [decimal](10, 2) NOT NULL,
	[StripePaymentReference] [nvarchar](255) NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[SmsSentCount] [int] NOT NULL,
	[RewardRedeemedForSeasonId] [int] NULL,
 CONSTRAINT [PK_SeasonPasses] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TournamentRoundMappings]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[TournamentRoundMappings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SeasonId] [int] NOT NULL,
	[RoundNumber] [int] NOT NULL,
	[DisplayName] [nvarchar](200) NOT NULL,
	[Stages] [nvarchar](500) NOT NULL,
	[ExpectedMatchCount] [int] NOT NULL,
 CONSTRAINT [PK_TournamentRoundMappings] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_TournamentRoundMappings_Season_Round] UNIQUE NONCLUSTERED 
(
	[SeasonId] ASC,
	[RoundNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Rounds]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Rounds](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SeasonId] [int] NOT NULL,
	[RoundNumber] [int] NOT NULL,
	[Status] [nvarchar](50) NOT NULL,
	[ApiRoundName] [nvarchar](128) NULL,
	[CompletedDate] [datetime2](7) NULL,
	[StartDateUtc] [datetime2](7) NOT NULL,
	[DeadlineUtc] [datetime2](7) NOT NULL,
	[LastReminderSentUtc] [datetime2](7) NULL,
	[CompletedDateUtc] [datetime2](7) NULL,
	[DisplayName] [nvarchar](200) NOT NULL,
	[ResultsDigestSentUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_Rounds] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Rounds_SeasonId_RoundNumber] UNIQUE NONCLUSTERED 
(
	[SeasonId] ASC,
	[RoundNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Leagues]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Leagues](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](150) NOT NULL,
	[SeasonId] [int] NOT NULL,
	[AdministratorUserId] [nvarchar](450) NOT NULL,
	[EntryCode] [nvarchar](10) NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[IsFree] [bit] NOT NULL,
	[HasPrizes] [bit] NOT NULL,
	[PrizeFundOverride] [decimal](18, 2) NULL,
	[PointsForExactScore] [int] NOT NULL,
	[PointsForCorrectResult] [int] NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[EntryDeadlineUtc] [datetime2](7) NULL,
	[BankAccountName] [nvarchar](512) NULL,
	[BankSortCode] [nvarchar](512) NULL,
	[BankAccountNumber] [nvarchar](512) NULL,
	[PaymentReferenceTemplate] [nvarchar](100) NULL,
	[RequiresMemberApproval] [bit] NOT NULL,
	[IsListed] [bit] NOT NULL,
 CONSTRAINT [PK_Leagues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Leagues_SeasonId_Name] UNIQUE NONCLUSTERED 
(
	[SeasonId] ASC,
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeagueRoundResults]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeagueRoundResults](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeagueId] [int] NOT NULL,
	[RoundId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[BasePoints] [int] NOT NULL,
	[BoostedPoints] [int] NOT NULL,
	[HasBoost] [bit] NOT NULL,
	[AppliedBoostCode] [nvarchar](50) NULL,
 CONSTRAINT [PK_LeagueRoundResults] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeaguePrizeSettings]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeaguePrizeSettings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeagueId] [int] NOT NULL,
	[PrizeType] [nvarchar](20) NOT NULL,
	[Rank] [int] NOT NULL,
	[PrizeAmount] [money] NOT NULL,
	[PrizeDescription] [nvarchar](255) NULL,
	[Stage] [nvarchar](50) NULL,
 CONSTRAINT [PK_LeaguePrizeSettings] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Matches]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Matches](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RoundId] [int] NOT NULL,
	[HomeTeamId] [int] NULL,
	[AwayTeamId] [int] NULL,
	[Status] [nvarchar](50) NOT NULL,
	[ActualHomeTeamScore] [int] NULL,
	[ActualAwayTeamScore] [int] NULL,
	[ExternalId] [int] NULL,
	[MatchDateTimeUtc] [datetime2](7) NOT NULL,
	[CustomLockTimeUtc] [datetime2](7) NULL,
	[PlaceholderHomeName] [nvarchar](100) NULL,
	[PlaceholderAwayName] [nvarchar](100) NULL,
	[MatchNumber] [int] NULL,
	[ApiRoundName] [nvarchar](128) NULL,
 CONSTRAINT [PK_Matches] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeagueWelcomeNotifications]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeagueWelcomeNotifications](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeagueId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[SentAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_LeagueWelcomeNotifications] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LeagueWelcomeNotifications_League_User] UNIQUE NONCLUSTERED 
(
	[LeagueId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeaguePrizeScheme]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeaguePrizeScheme](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeagueId] [int] NOT NULL,
	[SetAtUtc] [datetime2](7) NOT NULL,
	[SetByUserId] [nvarchar](450) NOT NULL,
 CONSTRAINT [PK_LeaguePrizeScheme] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LeaguePrizeScheme_LeagueId] UNIQUE NONCLUSTERED 
(
	[LeagueId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeaguePayouts]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeaguePayouts](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeagueId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[TotalAmount] [decimal](18, 2) NOT NULL,
	[PaidAtUtc] [datetime2](7) NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_LeaguePayouts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LeaguePayouts_League_User] UNIQUE NONCLUSTERED 
(
	[LeagueId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeagueMemberStats]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeagueMemberStats](
	[LeagueId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[OverallRank] [int] NOT NULL,
	[MonthRank] [int] NOT NULL,
	[LiveRoundRank] [int] NOT NULL,
	[SnapshotOverallRank] [int] NOT NULL,
	[SnapshotMonthRank] [int] NOT NULL,
	[StableRoundRank] [int] NOT NULL,
	[LiveRoundPoints] [decimal](10, 2) NOT NULL,
	[StableRoundPoints] [decimal](10, 2) NOT NULL,
 CONSTRAINT [PK_LeagueMemberStats] PRIMARY KEY CLUSTERED 
(
	[LeagueId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeagueMembers]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeagueMembers](
	[LeagueId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[IsAlertDismissed] [bit] NOT NULL,
	[JoinedAtUtc] [datetime2](7) NOT NULL,
	[ApprovedAtUtc] [datetime2](7) NULL,
	[IsArchivedByUser] [bit] NOT NULL,
 CONSTRAINT [PK_LeagueMembers] PRIMARY KEY CLUSTERED 
(
	[LeagueId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RoundResults]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[RoundResults](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RoundId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[TotalPoints] [int] NOT NULL,
	[ExactScoreCount] [int] NOT NULL,
	[CorrectResultCount] [int] NOT NULL,
	[IncorrectCount] [int] NOT NULL,
 CONSTRAINT [PK_RoundResults] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_RoundResults_RoundId_UserId] UNIQUE NONCLUSTERED 
(
	[RoundId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeagueBoostRules]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeagueBoostRules](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeagueId] [int] NOT NULL,
	[BoostDefinitionId] [int] NOT NULL,
	[TotalUsesPerSeason] [int] NOT NULL,
	[IsEnabled] [bit] NOT NULL,
 CONSTRAINT [PK_LeagueBoostRules] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LeagueBoostRules_League_Boost] UNIQUE NONCLUSTERED 
(
	[LeagueId] ASC,
	[BoostDefinitionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Winnings]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Winnings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[LeaguePrizeSettingId] [int] NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[RoundNumber] [int] NULL,
	[Month] [int] NULL,
	[AwardedDateUtc] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserPredictions]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[UserPredictions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[MatchId] [int] NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[PredictedHomeScore] [int] NOT NULL,
	[PredictedAwayScore] [int] NOT NULL,
	[PointsAwarded] [int] NULL,
	[Outcome] [int] NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_UserPredictions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_UserPredictions_MatchId_UserId] UNIQUE NONCLUSTERED 
(
	[MatchId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[UserBoostUsages](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[LeagueId] [int] NOT NULL,
	[SeasonId] [int] NOT NULL,
	[RoundId] [int] NULL,
	[MatchId] [int] NULL,
	[BoostDefinitionId] [int] NOT NULL,
	[PlayedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_UserBoostUsages] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UK_UserBoostUsages_UserLeagueRoundBoost] UNIQUE NONCLUSTERED 
(
	[UserId] ASC,
	[LeagueId] ASC,
	[RoundId] ASC,
	[BoostDefinitionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PrizeNotifications]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[PrizeNotifications](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) NOT NULL,
	[LeaguePrizeSettingId] [int] NOT NULL,
	[RoundNumber] [int] NULL,
	[Month] [int] NULL,
	[SentAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_PrizeNotifications] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeagueBoostWindows]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeagueBoostWindows](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeagueBoostRuleId] [int] NOT NULL,
	[StartRoundNumber] [int] NOT NULL,
	[EndRoundNumber] [int] NOT NULL,
	[MaxUsesInWindow] [int] NOT NULL,
 CONSTRAINT [PK_LeagueBoostWindows] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeaguePrizeSchemeEntries]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[LeaguePrizeSchemeEntries](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LeaguePrizeSchemeId] [int] NOT NULL,
	[Category] [nvarchar](20) NOT NULL,
	[PerEntryPounds] [int] NOT NULL,
	[RankTableJson] [nvarchar](max) NULL,
 CONSTRAINT [PK_LeaguePrizeSchemeEntries] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LeaguePrizeSchemeEntries_Category] UNIQUE NONCLUSTERED 
(
	[LeaguePrizeSchemeId] ASC,
	[Category] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Competitions]') AND name = N'UX_Competitions_Code')
CREATE UNIQUE NONCLUSTERED INDEX [UX_Competitions_Code] ON [dbo].[Competitions]
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[PasswordResetTokens]') AND name = N'IX_PasswordResetTokens_ExpiresAtUtc')
CREATE NONCLUSTERED INDEX [IX_PasswordResetTokens_ExpiresAtUtc] ON [dbo].[PasswordResetTokens]
(
	[ExpiresAtUtc] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[PasswordResetTokens]') AND name = N'IX_PasswordResetTokens_UserId')
CREATE NONCLUSTERED INDEX [IX_PasswordResetTokens_UserId] ON [dbo].[PasswordResetTokens]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[EmailConfirmationTokens]') AND name = N'IX_EmailConfirmationTokens_UserId')
CREATE NONCLUSTERED INDEX [IX_EmailConfirmationTokens_UserId] ON [dbo].[EmailConfirmationTokens]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SeasonPasses]') AND name = N'UX_SeasonPasses_User_Season')
CREATE UNIQUE NONCLUSTERED INDEX [UX_SeasonPasses_User_Season] ON [dbo].[SeasonPasses]
(
	[UserId] ASC,
	[SeasonId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[LeagueRoundResults]') AND name = N'IX_LeagueRoundResults_League_Round')
CREATE NONCLUSTERED INDEX [IX_LeagueRoundResults_League_Round] ON [dbo].[LeagueRoundResults]
(
	[LeagueId] ASC,
	[RoundId] ASC
)
INCLUDE([UserId],[BoostedPoints],[BasePoints]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[LeagueRoundResults]') AND name = N'IX_LeagueRoundResults_League_User')
CREATE NONCLUSTERED INDEX [IX_LeagueRoundResults_League_User] ON [dbo].[LeagueRoundResults]
(
	[LeagueId] ASC,
	[UserId] ASC
)
INCLUDE([BoostedPoints],[BasePoints]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[LeagueRoundResults]') AND name = N'UQ_LeagueRoundResults_League_Round_User')
CREATE UNIQUE NONCLUSTERED INDEX [UQ_LeagueRoundResults_League_Round_User] ON [dbo].[LeagueRoundResults]
(
	[LeagueId] ASC,
	[RoundId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]') AND name = N'IX_UserBoostUsages_LeagueRound')
CREATE NONCLUSTERED INDEX [IX_UserBoostUsages_LeagueRound] ON [dbo].[UserBoostUsages]
(
	[LeagueId] ASC,
	[RoundId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]') AND name = N'IX_UserBoostUsages_OneBoostPerLeagueRound')
CREATE UNIQUE NONCLUSTERED INDEX [IX_UserBoostUsages_OneBoostPerLeagueRound] ON [dbo].[UserBoostUsages]
(
	[UserId] ASC,
	[LeagueId] ASC,
	[RoundId] ASC
)
WHERE ([RoundId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]') AND name = N'IX_UserBoostUsages_UserLeagueSeasonBoost')
CREATE NONCLUSTERED INDEX [IX_UserBoostUsages_UserLeagueSeasonBoost] ON [dbo].[UserBoostUsages]
(
	[UserId] ASC,
	[LeagueId] ASC,
	[SeasonId] ASC,
	[BoostDefinitionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[PrizeNotifications]') AND name = N'UX_PrizeNotifications_Winning')
CREATE UNIQUE NONCLUSTERED INDEX [UX_PrizeNotifications_Winning] ON [dbo].[PrizeNotifications]
(
	[UserId] ASC,
	[LeaguePrizeSettingId] ASC,
	[RoundNumber] ASC,
	[Month] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_RunningCosts_CreatedAtUtc]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[RunningCosts] ADD  CONSTRAINT [DF_RunningCosts_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_AspNetUsers_PreferredTheme]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[AspNetUsers] ADD  CONSTRAINT [DF_AspNetUsers_PreferredTheme]  DEFAULT ('light') FOR [PreferredTheme]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Seasons_IsActive]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Seasons] ADD  CONSTRAINT [DF_Seasons_IsActive]  DEFAULT ((1)) FOR [IsActive]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Seasons_NumberOfRounds]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Seasons] ADD  CONSTRAINT [DF_Seasons_NumberOfRounds]  DEFAULT ((0)) FOR [NumberOfRounds]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_SeasonPasses_SmsSentCount]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[SeasonPasses] ADD  CONSTRAINT [DF_SeasonPasses_SmsSentCount]  DEFAULT ((0)) FOR [SmsSentCount]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Rounds_Status]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Rounds] ADD  CONSTRAINT [DF_Rounds_Status]  DEFAULT ('Draft') FOR [Status]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Rounds_DisplayName]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Rounds] ADD  CONSTRAINT [DF_Rounds_DisplayName]  DEFAULT ('') FOR [DisplayName]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_Price]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_Price]  DEFAULT ((0)) FOR [Price]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_IsFree]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_IsFree]  DEFAULT ((0)) FOR [IsFree]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_HasPrizes]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_HasPrizes]  DEFAULT ((1)) FOR [HasPrizes]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_PointsForExactScore]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_PointsForExactScore]  DEFAULT ((5)) FOR [PointsForExactScore]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_PointsForCorrectResult]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_PointsForCorrectResult]  DEFAULT ((3)) FOR [PointsForCorrectResult]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_CreatedAtUtc]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_CreatedAtUtc]  DEFAULT (getutcdate()) FOR [CreatedAtUtc]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_RequiresMemberApproval]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_RequiresMemberApproval]  DEFAULT ((1)) FOR [RequiresMemberApproval]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Leagues_IsListed]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Leagues] ADD  CONSTRAINT [DF_Leagues_IsListed]  DEFAULT ((0)) FOR [IsListed]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueRoundResults_HasBoost]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueRoundResults] ADD  CONSTRAINT [DF_LeagueRoundResults_HasBoost]  DEFAULT ((0)) FOR [HasBoost]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Matches_Status]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Matches] ADD  CONSTRAINT [DF_Matches_Status]  DEFAULT ('Scheduled') FOR [Status]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_OverallRank]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_OverallRank]  DEFAULT ((0)) FOR [OverallRank]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_MonthRank]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_MonthRank]  DEFAULT ((0)) FOR [MonthRank]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_LiveRoundRank]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_LiveRoundRank]  DEFAULT ((0)) FOR [LiveRoundRank]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_SnapshotOverallRank]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_SnapshotOverallRank]  DEFAULT ((0)) FOR [SnapshotOverallRank]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_SnapshotMonthRank]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_SnapshotMonthRank]  DEFAULT ((0)) FOR [SnapshotMonthRank]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_StableRoundRank]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_StableRoundRank]  DEFAULT ((0)) FOR [StableRoundRank]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_LiveRoundPoints]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_LiveRoundPoints]  DEFAULT ((0.00)) FOR [LiveRoundPoints]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMemberStats_StableRoundPoints]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMemberStats] ADD  CONSTRAINT [DF_LeagueMemberStats_StableRoundPoints]  DEFAULT ((0.00)) FOR [StableRoundPoints]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMembers_Status]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMembers] ADD  CONSTRAINT [DF_LeagueMembers_Status]  DEFAULT ('Pending') FOR [Status]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMembers_IsAlertDismissed]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMembers] ADD  CONSTRAINT [DF_LeagueMembers_IsAlertDismissed]  DEFAULT ((0)) FOR [IsAlertDismissed]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMembers_JoinedAtUtc]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMembers] ADD  CONSTRAINT [DF_LeagueMembers_JoinedAtUtc]  DEFAULT (getutcdate()) FOR [JoinedAtUtc]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueMembers_IsArchivedByUser]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueMembers] ADD  CONSTRAINT [DF_LeagueMembers_IsArchivedByUser]  DEFAULT ((0)) FOR [IsArchivedByUser]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_RoundResults_TotalPoints]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[RoundResults] ADD  CONSTRAINT [DF_RoundResults_TotalPoints]  DEFAULT ((0)) FOR [TotalPoints]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_RoundResults_ExactScoreCount]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[RoundResults] ADD  CONSTRAINT [DF_RoundResults_ExactScoreCount]  DEFAULT ((0)) FOR [ExactScoreCount]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_RoundResults_CorrectResultCount]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[RoundResults] ADD  CONSTRAINT [DF_RoundResults_CorrectResultCount]  DEFAULT ((0)) FOR [CorrectResultCount]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_RoundResults_IncorrectCount]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[RoundResults] ADD  CONSTRAINT [DF_RoundResults_IncorrectCount]  DEFAULT ((0)) FOR [IncorrectCount]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_LeagueBoostRules_IsEnabled]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[LeagueBoostRules] ADD  CONSTRAINT [DF_LeagueBoostRules_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Winnings_AwardedDateUtc]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Winnings] ADD  CONSTRAINT [DF_Winnings_AwardedDateUtc]  DEFAULT (getutcdate()) FOR [AwardedDateUtc]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_UserPredictions_Outcome]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[UserPredictions] ADD  CONSTRAINT [DF_UserPredictions_Outcome]  DEFAULT ((0)) FOR [Outcome]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_UserPredictions_CreatedAtUtc]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[UserPredictions] ADD  CONSTRAINT [DF_UserPredictions_CreatedAtUtc]  DEFAULT (getutcdate()) FOR [CreatedAtUtc]
END

GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_UserBoostUsages_PlayedAtUtc]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[UserBoostUsages] ADD  CONSTRAINT [DF_UserBoostUsages_PlayedAtUtc]  DEFAULT (getutcdate()) FOR [PlayedAtUtc]
END

GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_AspNetUserRoles_AspNetRoles_RoleId]') AND parent_object_id = OBJECT_ID(N'[dbo].[AspNetUserRoles]'))
ALTER TABLE [dbo].[AspNetUserRoles]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[AspNetRoles] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_AspNetUserRoles_AspNetUsers_UserId]') AND parent_object_id = OBJECT_ID(N'[dbo].[AspNetUserRoles]'))
ALTER TABLE [dbo].[AspNetUserRoles]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_AspNetUserLogins_AspNetUsers_UserId]') AND parent_object_id = OBJECT_ID(N'[dbo].[AspNetUserLogins]'))
ALTER TABLE [dbo].[AspNetUserLogins]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_AspNetUserClaims_AspNetUsers_UserId]') AND parent_object_id = OBJECT_ID(N'[dbo].[AspNetUserClaims]'))
ALTER TABLE [dbo].[AspNetUserClaims]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_AspNetUserTokens_AspNetUsers_UserId]') AND parent_object_id = OBJECT_ID(N'[dbo].[AspNetUserTokens]'))
ALTER TABLE [dbo].[AspNetUserTokens]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_AspNetRoleClaims_AspNetRoles_RoleId]') AND parent_object_id = OBJECT_ID(N'[dbo].[AspNetRoleClaims]'))
ALTER TABLE [dbo].[AspNetRoleClaims]  WITH CHECK ADD  CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[AspNetRoles] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_PasswordResetTokens_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[PasswordResetTokens]'))
ALTER TABLE [dbo].[PasswordResetTokens]  WITH CHECK ADD  CONSTRAINT [FK_PasswordResetTokens_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_RefreshTokens_AspNetUsers_UserId]') AND parent_object_id = OBJECT_ID(N'[dbo].[RefreshTokens]'))
ALTER TABLE [dbo].[RefreshTokens]  WITH CHECK ADD  CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserPayoutDetails_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserPayoutDetails]'))
ALTER TABLE [dbo].[UserPayoutDetails]  WITH CHECK ADD  CONSTRAINT [FK_UserPayoutDetails_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserOnboardingSkips_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserOnboardingSkips]'))
ALTER TABLE [dbo].[UserOnboardingSkips]  WITH CHECK ADD  CONSTRAINT [FK_UserOnboardingSkips_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_EmailConfirmationTokens_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[EmailConfirmationTokens]'))
ALTER TABLE [dbo].[EmailConfirmationTokens]  WITH CHECK ADD  CONSTRAINT [FK_EmailConfirmationTokens_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Seasons_Competitions]') AND parent_object_id = OBJECT_ID(N'[dbo].[Seasons]'))
ALTER TABLE [dbo].[Seasons]  WITH CHECK ADD  CONSTRAINT [FK_Seasons_Competitions] FOREIGN KEY([CompetitionId])
REFERENCES [dbo].[Competitions] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_SeasonPasses_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[SeasonPasses]'))
ALTER TABLE [dbo].[SeasonPasses]  WITH CHECK ADD  CONSTRAINT [FK_SeasonPasses_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_SeasonPasses_Seasons]') AND parent_object_id = OBJECT_ID(N'[dbo].[SeasonPasses]'))
ALTER TABLE [dbo].[SeasonPasses]  WITH CHECK ADD  CONSTRAINT [FK_SeasonPasses_Seasons] FOREIGN KEY([SeasonId])
REFERENCES [dbo].[Seasons] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_SeasonPasses_Seasons_Reward]') AND parent_object_id = OBJECT_ID(N'[dbo].[SeasonPasses]'))
ALTER TABLE [dbo].[SeasonPasses]  WITH CHECK ADD  CONSTRAINT [FK_SeasonPasses_Seasons_Reward] FOREIGN KEY([RewardRedeemedForSeasonId])
REFERENCES [dbo].[Seasons] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_TournamentRoundMappings_Seasons]') AND parent_object_id = OBJECT_ID(N'[dbo].[TournamentRoundMappings]'))
ALTER TABLE [dbo].[TournamentRoundMappings]  WITH CHECK ADD  CONSTRAINT [FK_TournamentRoundMappings_Seasons] FOREIGN KEY([SeasonId])
REFERENCES [dbo].[Seasons] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Rounds_Seasons]') AND parent_object_id = OBJECT_ID(N'[dbo].[Rounds]'))
ALTER TABLE [dbo].[Rounds]  WITH CHECK ADD  CONSTRAINT [FK_Rounds_Seasons] FOREIGN KEY([SeasonId])
REFERENCES [dbo].[Seasons] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Leagues_Seasons]') AND parent_object_id = OBJECT_ID(N'[dbo].[Leagues]'))
ALTER TABLE [dbo].[Leagues]  WITH CHECK ADD  CONSTRAINT [FK_Leagues_Seasons] FOREIGN KEY([SeasonId])
REFERENCES [dbo].[Seasons] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Leagues_Users_Admin]') AND parent_object_id = OBJECT_ID(N'[dbo].[Leagues]'))
ALTER TABLE [dbo].[Leagues]  WITH CHECK ADD  CONSTRAINT [FK_Leagues_Users_Admin] FOREIGN KEY([AdministratorUserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueRoundResults_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueRoundResults]'))
ALTER TABLE [dbo].[LeagueRoundResults]  WITH CHECK ADD  CONSTRAINT [FK_LeagueRoundResults_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueRoundResults_Rounds]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueRoundResults]'))
ALTER TABLE [dbo].[LeagueRoundResults]  WITH CHECK ADD  CONSTRAINT [FK_LeagueRoundResults_Rounds] FOREIGN KEY([RoundId])
REFERENCES [dbo].[Rounds] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueRoundResults_Users]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueRoundResults]'))
ALTER TABLE [dbo].[LeagueRoundResults]  WITH CHECK ADD  CONSTRAINT [FK_LeagueRoundResults_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeaguePrizeSettings_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeaguePrizeSettings]'))
ALTER TABLE [dbo].[LeaguePrizeSettings]  WITH CHECK ADD  CONSTRAINT [FK_LeaguePrizeSettings_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Matches_AwayTeam]') AND parent_object_id = OBJECT_ID(N'[dbo].[Matches]'))
ALTER TABLE [dbo].[Matches]  WITH CHECK ADD  CONSTRAINT [FK_Matches_AwayTeam] FOREIGN KEY([AwayTeamId])
REFERENCES [dbo].[Teams] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Matches_HomeTeam]') AND parent_object_id = OBJECT_ID(N'[dbo].[Matches]'))
ALTER TABLE [dbo].[Matches]  WITH CHECK ADD  CONSTRAINT [FK_Matches_HomeTeam] FOREIGN KEY([HomeTeamId])
REFERENCES [dbo].[Teams] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Matches_Rounds]') AND parent_object_id = OBJECT_ID(N'[dbo].[Matches]'))
ALTER TABLE [dbo].[Matches]  WITH CHECK ADD  CONSTRAINT [FK_Matches_Rounds] FOREIGN KEY([RoundId])
REFERENCES [dbo].[Rounds] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueWelcomeNotifications_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueWelcomeNotifications]'))
ALTER TABLE [dbo].[LeagueWelcomeNotifications]  WITH CHECK ADD  CONSTRAINT [FK_LeagueWelcomeNotifications_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueWelcomeNotifications_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueWelcomeNotifications]'))
ALTER TABLE [dbo].[LeagueWelcomeNotifications]  WITH CHECK ADD  CONSTRAINT [FK_LeagueWelcomeNotifications_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeaguePrizeScheme_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeaguePrizeScheme]'))
ALTER TABLE [dbo].[LeaguePrizeScheme]  WITH CHECK ADD  CONSTRAINT [FK_LeaguePrizeScheme_AspNetUsers] FOREIGN KEY([SetByUserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeaguePrizeScheme_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeaguePrizeScheme]'))
ALTER TABLE [dbo].[LeaguePrizeScheme]  WITH CHECK ADD  CONSTRAINT [FK_LeaguePrizeScheme_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeaguePayouts_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeaguePayouts]'))
ALTER TABLE [dbo].[LeaguePayouts]  WITH CHECK ADD  CONSTRAINT [FK_LeaguePayouts_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeaguePayouts_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeaguePayouts]'))
ALTER TABLE [dbo].[LeaguePayouts]  WITH CHECK ADD  CONSTRAINT [FK_LeaguePayouts_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueMemberStats_League]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueMemberStats]'))
ALTER TABLE [dbo].[LeagueMemberStats]  WITH CHECK ADD  CONSTRAINT [FK_LeagueMemberStats_League] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueMemberStats_User]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueMemberStats]'))
ALTER TABLE [dbo].[LeagueMemberStats]  WITH CHECK ADD  CONSTRAINT [FK_LeagueMemberStats_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueMembers_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueMembers]'))
ALTER TABLE [dbo].[LeagueMembers]  WITH CHECK ADD  CONSTRAINT [FK_LeagueMembers_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueMembers_Users]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueMembers]'))
ALTER TABLE [dbo].[LeagueMembers]  WITH CHECK ADD  CONSTRAINT [FK_LeagueMembers_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_RoundResults_Rounds]') AND parent_object_id = OBJECT_ID(N'[dbo].[RoundResults]'))
ALTER TABLE [dbo].[RoundResults]  WITH CHECK ADD  CONSTRAINT [FK_RoundResults_Rounds] FOREIGN KEY([RoundId])
REFERENCES [dbo].[Rounds] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_RoundResults_Users]') AND parent_object_id = OBJECT_ID(N'[dbo].[RoundResults]'))
ALTER TABLE [dbo].[RoundResults]  WITH CHECK ADD  CONSTRAINT [FK_RoundResults_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueBoostRules_BoostDefinitions]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueBoostRules]'))
ALTER TABLE [dbo].[LeagueBoostRules]  WITH CHECK ADD  CONSTRAINT [FK_LeagueBoostRules_BoostDefinitions] FOREIGN KEY([BoostDefinitionId])
REFERENCES [dbo].[BoostDefinitions] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueBoostRules_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueBoostRules]'))
ALTER TABLE [dbo].[LeagueBoostRules]  WITH CHECK ADD  CONSTRAINT [FK_LeagueBoostRules_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Winnings_AspNetUsers]') AND parent_object_id = OBJECT_ID(N'[dbo].[Winnings]'))
ALTER TABLE [dbo].[Winnings]  WITH CHECK ADD  CONSTRAINT [FK_Winnings_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Winnings_LeaguePrizeSettings]') AND parent_object_id = OBJECT_ID(N'[dbo].[Winnings]'))
ALTER TABLE [dbo].[Winnings]  WITH CHECK ADD  CONSTRAINT [FK_Winnings_LeaguePrizeSettings] FOREIGN KEY([LeaguePrizeSettingId])
REFERENCES [dbo].[LeaguePrizeSettings] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserPredictions_Matches]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserPredictions]'))
ALTER TABLE [dbo].[UserPredictions]  WITH CHECK ADD  CONSTRAINT [FK_UserPredictions_Matches] FOREIGN KEY([MatchId])
REFERENCES [dbo].[Matches] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserPredictions_Users]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserPredictions]'))
ALTER TABLE [dbo].[UserPredictions]  WITH CHECK ADD  CONSTRAINT [FK_UserPredictions_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserBoostUsages_BoostDefinitions]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]'))
ALTER TABLE [dbo].[UserBoostUsages]  WITH CHECK ADD  CONSTRAINT [FK_UserBoostUsages_BoostDefinitions] FOREIGN KEY([BoostDefinitionId])
REFERENCES [dbo].[BoostDefinitions] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserBoostUsages_Leagues]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]'))
ALTER TABLE [dbo].[UserBoostUsages]  WITH CHECK ADD  CONSTRAINT [FK_UserBoostUsages_Leagues] FOREIGN KEY([LeagueId])
REFERENCES [dbo].[Leagues] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserBoostUsages_Matches]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]'))
ALTER TABLE [dbo].[UserBoostUsages]  WITH CHECK ADD  CONSTRAINT [FK_UserBoostUsages_Matches] FOREIGN KEY([MatchId])
REFERENCES [dbo].[Matches] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserBoostUsages_Rounds]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]'))
ALTER TABLE [dbo].[UserBoostUsages]  WITH CHECK ADD  CONSTRAINT [FK_UserBoostUsages_Rounds] FOREIGN KEY([RoundId])
REFERENCES [dbo].[Rounds] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserBoostUsages_Seasons]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]'))
ALTER TABLE [dbo].[UserBoostUsages]  WITH CHECK ADD  CONSTRAINT [FK_UserBoostUsages_Seasons] FOREIGN KEY([SeasonId])
REFERENCES [dbo].[Seasons] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_UserBoostUsages_Users]') AND parent_object_id = OBJECT_ID(N'[dbo].[UserBoostUsages]'))
ALTER TABLE [dbo].[UserBoostUsages]  WITH CHECK ADD  CONSTRAINT [FK_UserBoostUsages_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_PrizeNotifications_AspNetUsers_UserId]') AND parent_object_id = OBJECT_ID(N'[dbo].[PrizeNotifications]'))
ALTER TABLE [dbo].[PrizeNotifications]  WITH CHECK ADD  CONSTRAINT [FK_PrizeNotifications_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_PrizeNotifications_LeaguePrizeSettings_LeaguePrizeSettingId]') AND parent_object_id = OBJECT_ID(N'[dbo].[PrizeNotifications]'))
ALTER TABLE [dbo].[PrizeNotifications]  WITH CHECK ADD  CONSTRAINT [FK_PrizeNotifications_LeaguePrizeSettings_LeaguePrizeSettingId] FOREIGN KEY([LeaguePrizeSettingId])
REFERENCES [dbo].[LeaguePrizeSettings] ([Id])
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeagueBoostWindows_LeagueBoostRules]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeagueBoostWindows]'))
ALTER TABLE [dbo].[LeagueBoostWindows]  WITH CHECK ADD  CONSTRAINT [FK_LeagueBoostWindows_LeagueBoostRules] FOREIGN KEY([LeagueBoostRuleId])
REFERENCES [dbo].[LeagueBoostRules] ([Id])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_LeaguePrizeSchemeEntries_Scheme]') AND parent_object_id = OBJECT_ID(N'[dbo].[LeaguePrizeSchemeEntries]'))
ALTER TABLE [dbo].[LeaguePrizeSchemeEntries]  WITH CHECK ADD  CONSTRAINT [FK_LeaguePrizeSchemeEntries_Scheme] FOREIGN KEY([LeaguePrizeSchemeId])
REFERENCES [dbo].[LeaguePrizeScheme] ([Id])
ON DELETE CASCADE
GO


