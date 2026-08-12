using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Persistence.Conformance;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;

/// <summary>
/// Inserts rows directly, bypassing the repositories, so a test arranges its world without depending
/// on the write path it is about to assert on. Every method returns the identity the database
/// generated - tests never assume an id, because Respawn does not reseed identities between tests.
///
/// Columns are written explicitly rather than via a domain entity so that a schema change shows up
/// here as a compile-or-run failure in one place, and so a test can arrange states the domain
/// deliberately forbids (an unconfirmed fixture, a postponed match) which is exactly what the
/// predicates under test have to cope with.
/// </summary>
internal sealed class TestDataSeeder(IDbConnectionFactory connectionFactory) : ITestDataSeeder
{
    private const string DefaultTheme = "light";

    /// <summary>
    /// The shared backdrop nearly every test needs: a competition, an active season, two teams to
    /// play each other, and one player.
    /// </summary>
    public async Task<SeededBackdrop> AddBackdropAsync()
    {
        var competitionId = await AddCompetitionAsync();
        var seasonId = await AddSeasonAsync(competitionId);
        var homeTeamId = await AddTeamAsync("Arsenal", "ARS");
        var awayTeamId = await AddTeamAsync("Chelsea", "CHE");
        var userId = await AddUserAsync("Ada", "Lovelace");

        return new SeededBackdrop(competitionId, seasonId, homeTeamId, awayTeamId, userId);
    }

    public async Task<int> AddCompetitionAsync(string code = "TEST")
    {
        const string sql = @"
            INSERT INTO [Competitions]
            (
                [Code],
                [Name],
                [Type],
                [CreatedAtUtc]
            )
            VALUES
            (
                @Code,
                @Name,
                @Type,
                @CreatedAtUtc
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            Code = code,
            Name = $"Competition {code}",
            Type = 0,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task<int> AddSeasonAsync(int competitionId, string name = "2026/27", int numberOfRounds = 38, bool isActive = true)
    {
        const string sql = @"
            INSERT INTO [Seasons]
            (
                [Name],
                [IsActive],
                [NumberOfRounds],
                [StartDateUtc],
                [EndDateUtc],
                [CompetitionId]
            )
            VALUES
            (
                @Name,
                @IsActive,
                @NumberOfRounds,
                @StartDateUtc,
                @EndDateUtc,
                @CompetitionId
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            Name = name,
            IsActive = isActive,
            NumberOfRounds = numberOfRounds,
            StartDateUtc = DateTime.UtcNow.AddMonths(-1),
            EndDateUtc = DateTime.UtcNow.AddMonths(9),
            CompetitionId = competitionId
        });
    }

    public async Task<int> AddTeamAsync(string name, string abbreviation)
    {
        const string sql = @"
            INSERT INTO [Teams]
            (
                [Name],
                [Abbreviation],
                [ShortName]
            )
            VALUES
            (
                @Name,
                @Abbreviation,
                @ShortName
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            Name = name,
            Abbreviation = abbreviation,
            ShortName = name.Length <= 16 ? name : name[..16]
        });
    }

    public async Task<string> AddUserAsync(string firstName, string lastName)
    {
        var userId = Guid.NewGuid().ToString();
        var email = $"{firstName}.{lastName}@integration.test".ToLowerInvariant();

        const string sql = @"
            INSERT INTO [AspNetUsers]
            (
                [Id],
                [UserName],
                [NormalizedUserName],
                [Email],
                [NormalizedEmail],
                [EmailConfirmed],
                [PhoneNumberConfirmed],
                [TwoFactorEnabled],
                [LockoutEnabled],
                [AccessFailedCount],
                [FirstName],
                [LastName],
                [PreferredTheme]
            )
            VALUES
            (
                @Id,
                @Email,
                @NormalizedEmail,
                @Email,
                @NormalizedEmail,
                @EmailConfirmed,
                @PhoneNumberConfirmed,
                @TwoFactorEnabled,
                @LockoutEnabled,
                @AccessFailedCount,
                @FirstName,
                @LastName,
                @PreferredTheme
            );";

        await ExecuteAsync(sql, new
        {
            Id = userId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnabled = false,
            AccessFailedCount = 0,
            FirstName = firstName,
            LastName = lastName,
            PreferredTheme = DefaultTheme
        });

        return userId;
    }

    public async Task<int> AddRoundAsync(
        int seasonId,
        int roundNumber,
        DateTime deadlineUtc,
        RoundStatus status = RoundStatus.Published,
        DateTime? startDateUtc = null,
        DateTime? completedDateUtc = null,
        string? displayName = null)
    {
        const string sql = @"
            INSERT INTO [Rounds]
            (
                [SeasonId],
                [RoundNumber],
                [DisplayName],
                [Status],
                [StartDateUtc],
                [DeadlineUtc],
                [CompletedDateUtc]
            )
            VALUES
            (
                @SeasonId,
                @RoundNumber,
                @DisplayName,
                @Status,
                @StartDateUtc,
                @DeadlineUtc,
                @CompletedDateUtc
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            SeasonId = seasonId,
            RoundNumber = roundNumber,
            DisplayName = displayName ?? $"Round {roundNumber}",
            Status = status.ToString(),
            StartDateUtc = startDateUtc ?? deadlineUtc.AddDays(-1),
            DeadlineUtc = deadlineUtc,
            CompletedDateUtc = completedDateUtc
        });
    }

    public async Task<int> AddMatchAsync(
        int roundId,
        int? homeTeamId,
        int? awayTeamId,
        DateTime? matchDateTimeUtc = null,
        DateTime? customLockTimeUtc = null,
        MatchStatus status = MatchStatus.Scheduled,
        int? matchNumber = null)
    {
        const string sql = @"
            INSERT INTO [Matches]
            (
                [RoundId],
                [HomeTeamId],
                [AwayTeamId],
                [Status],
                [MatchDateTimeUtc],
                [CustomLockTimeUtc],
                [MatchNumber]
            )
            VALUES
            (
                @RoundId,
                @HomeTeamId,
                @AwayTeamId,
                @Status,
                @MatchDateTimeUtc,
                @CustomLockTimeUtc,
                @MatchNumber
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            RoundId = roundId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Status = status.ToString(),
            MatchDateTimeUtc = matchDateTimeUtc ?? DateTime.UtcNow.AddDays(1),
            CustomLockTimeUtc = customLockTimeUtc,
            MatchNumber = matchNumber
        });
    }

    public async Task AddPredictionAsync(
        int matchId,
        string userId,
        int homeScore = 2,
        int awayScore = 1,
        PredictionOutcome outcome = PredictionOutcome.Pending)
    {
        const string sql = @"
            INSERT INTO [UserPredictions]
            (
                [MatchId],
                [UserId],
                [PredictedHomeScore],
                [PredictedAwayScore],
                [Outcome],
                [CreatedAtUtc]
            )
            VALUES
            (
                @MatchId,
                @UserId,
                @PredictedHomeScore,
                @PredictedAwayScore,
                @Outcome,
                @CreatedAtUtc
            );";

        await ExecuteAsync(sql, new
        {
            MatchId = matchId,
            UserId = userId,
            PredictedHomeScore = homeScore,
            PredictedAwayScore = awayScore,
            Outcome = (int)outcome,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task<int> AddLeagueAsync(
        int seasonId,
        string administratorUserId,
        string name = "Integration League",
        bool hasPrizes = false)
    {
        const string sql = @"
            INSERT INTO [Leagues]
            (
                [Name],
                [SeasonId],
                [AdministratorUserId],
                [Price],
                [IsFree],
                [HasPrizes],
                [PointsForExactScore],
                [PointsForCorrectResult],
                [CreatedAtUtc],
                [RequiresMemberApproval],
                [IsListed]
            )
            VALUES
            (
                @Name,
                @SeasonId,
                @AdministratorUserId,
                @Price,
                @IsFree,
                @HasPrizes,
                @PointsForExactScore,
                @PointsForCorrectResult,
                @CreatedAtUtc,
                @RequiresMemberApproval,
                @IsListed
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            Name = name,
            SeasonId = seasonId,
            AdministratorUserId = administratorUserId,
            Price = 0m,
            IsFree = true,
            HasPrizes = hasPrizes,
            PointsForExactScore = 3,
            PointsForCorrectResult = 1,
            CreatedAtUtc = DateTime.UtcNow,
            RequiresMemberApproval = false,
            IsListed = false
        });
    }

    public async Task AddLeagueMemberAsync(
        int leagueId,
        string userId,
        LeagueMemberStatus status = LeagueMemberStatus.Approved)
    {
        const string sql = @"
            INSERT INTO [LeagueMembers]
            (
                [LeagueId],
                [UserId],
                [Status],
                [IsAlertDismissed],
                [JoinedAtUtc],
                [IsArchivedByUser]
            )
            VALUES
            (
                @LeagueId,
                @UserId,
                @Status,
                @IsAlertDismissed,
                @JoinedAtUtc,
                @IsArchivedByUser
            );";

        await ExecuteAsync(sql, new
        {
            LeagueId = leagueId,
            UserId = userId,
            Status = status.ToString(),
            IsAlertDismissed = false,
            JoinedAtUtc = DateTime.UtcNow,
            IsArchivedByUser = false
        });
    }

    public async Task<int> AddBoostDefinitionAsync(string code, string name, string scope = "Round")
    {
        const string sql = @"
            INSERT INTO [BoostDefinitions]
            (
                [Code],
                [Name],
                [Scope],
                [ImageUrl]
            )
            VALUES
            (
                @Code,
                @Name,
                @Scope,
                @ImageUrl
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            Code = code,
            Name = name,
            Scope = scope,
            ImageUrl = $"/images/boosts/{code.ToLowerInvariant()}.webp"
        });
    }

    public async Task<int> AddLeagueBoostRuleAsync(
        int leagueId,
        int boostDefinitionId,
        int totalUsesPerSeason = 2,
        bool isEnabled = true)
    {
        const string sql = @"
            INSERT INTO [LeagueBoostRules]
            (
                [LeagueId],
                [BoostDefinitionId],
                [TotalUsesPerSeason],
                [IsEnabled]
            )
            VALUES
            (
                @LeagueId,
                @BoostDefinitionId,
                @TotalUsesPerSeason,
                @IsEnabled
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            LeagueId = leagueId,
            BoostDefinitionId = boostDefinitionId,
            TotalUsesPerSeason = totalUsesPerSeason,
            IsEnabled = isEnabled
        });
    }

    public async Task AddBoostUsageAsync(
        string userId,
        int leagueId,
        int seasonId,
        int roundId,
        int boostDefinitionId)
    {
        const string sql = @"
            INSERT INTO [UserBoostUsages]
            (
                [UserId],
                [LeagueId],
                [SeasonId],
                [RoundId],
                [BoostDefinitionId],
                [PlayedAtUtc]
            )
            VALUES
            (
                @UserId,
                @LeagueId,
                @SeasonId,
                @RoundId,
                @BoostDefinitionId,
                @PlayedAtUtc
            );";

        await ExecuteAsync(sql, new
        {
            UserId = userId,
            LeagueId = leagueId,
            SeasonId = seasonId,
            RoundId = roundId,
            BoostDefinitionId = boostDefinitionId,
            PlayedAtUtc = DateTime.UtcNow
        });
    }

    public async Task AddLeagueRoundResultAsync(
        int leagueId,
        int roundId,
        string userId,
        int basePoints,
        int boostedPoints,
        string appliedBoostCode)
    {
        const string sql = @"
            INSERT INTO [LeagueRoundResults]
            (
                [LeagueId],
                [RoundId],
                [UserId],
                [BasePoints],
                [BoostedPoints],
                [HasBoost],
                [AppliedBoostCode]
            )
            VALUES
            (
                @LeagueId,
                @RoundId,
                @UserId,
                @BasePoints,
                @BoostedPoints,
                @HasBoost,
                @AppliedBoostCode
            );";

        await ExecuteAsync(sql, new
        {
            LeagueId = leagueId,
            RoundId = roundId,
            UserId = userId,
            BasePoints = basePoints,
            BoostedPoints = boostedPoints,
            HasBoost = true,
            AppliedBoostCode = appliedBoostCode
        });
    }

    public async Task AddTournamentRoundMappingAsync(int seasonId, int roundNumber, string stages)
    {
        const string sql = @"
            INSERT INTO [TournamentRoundMappings]
            (
                [SeasonId],
                [RoundNumber],
                [DisplayName],
                [Stages],
                [ExpectedMatchCount]
            )
            VALUES
            (
                @SeasonId,
                @RoundNumber,
                @DisplayName,
                @Stages,
                @ExpectedMatchCount
            );";

        await ExecuteAsync(sql, new
        {
            SeasonId = seasonId,
            RoundNumber = roundNumber,
            DisplayName = stages,
            Stages = stages,
            ExpectedMatchCount = 0
        });
    }

    public async Task AddRoundResultAsync(
        int roundId, string userId, int exactScoreCount, int correctResultCount = 0, int incorrectCount = 0)
    {
        // TotalPoints is a vestigial NOT NULL column that migration 0005 dropped from the schema's intent but
        // which still exists with a DEFAULT of 0; nothing reads it (see 0005_DropRoundResultsTotalPoints.sql).
        const string sql = @"
            INSERT INTO [RoundResults]
            (
                [RoundId],
                [UserId],
                [ExactScoreCount],
                [CorrectResultCount],
                [IncorrectCount]
            )
            VALUES
            (
                @RoundId,
                @UserId,
                @ExactScoreCount,
                @CorrectResultCount,
                @IncorrectCount
            );";

        await ExecuteAsync(sql, new
        {
            RoundId = roundId,
            UserId = userId,
            ExactScoreCount = exactScoreCount,
            CorrectResultCount = correctResultCount,
            IncorrectCount = incorrectCount
        });
    }

    public async Task<int> AddLeaguePrizeSettingAsync(
        int leagueId,
        PrizeType prizeType,
        decimal prizeAmount,
        int rank = 1,
        string? prizeDescription = null)
    {
        // [PrizeType] is nvarchar but holds the enum's numeric value, because the write path passes the enum and
        // Dapper sends its underlying int. Reproduced deliberately - see ITestDataSeeder.
        const string sql = @"
            INSERT INTO [LeaguePrizeSettings]
            (
                [LeagueId],
                [PrizeType],
                [Rank],
                [PrizeAmount],
                [PrizeDescription]
            )
            VALUES
            (
                @LeagueId,
                @PrizeType,
                @Rank,
                @PrizeAmount,
                @PrizeDescription
            );

            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            LeagueId = leagueId,
            PrizeType = (int)prizeType,
            Rank = rank,
            PrizeAmount = prizeAmount,
            PrizeDescription = prizeDescription
        });
    }

    public async Task AddWinningAsync(
        string userId,
        int leaguePrizeSettingId,
        decimal amount,
        DateTime? awardedDateUtc = null,
        int? roundNumber = null,
        int? month = null)
    {
        const string sql = @"
            INSERT INTO [Winnings]
            (
                [UserId],
                [LeaguePrizeSettingId],
                [Amount],
                [RoundNumber],
                [Month],
                [AwardedDateUtc]
            )
            VALUES
            (
                @UserId,
                @LeaguePrizeSettingId,
                @Amount,
                @RoundNumber,
                @Month,
                @AwardedDateUtc
            );";

        await ExecuteAsync(sql, new
        {
            UserId = userId,
            LeaguePrizeSettingId = leaguePrizeSettingId,
            Amount = amount,
            RoundNumber = roundNumber,
            Month = month,
            AwardedDateUtc = awardedDateUtc ?? DateTime.UtcNow
        });
    }

    public async Task AddLeagueMemberStatsAsync(
        int leagueId,
        string userId,
        int? overallRank = null,
        int? monthRank = null,
        int? liveRoundRank = null,
        int? snapshotOverallRank = null,
        int? snapshotMonthRank = null,
        int? stableRoundRank = null,
        int? stageRank = null,
        int? preRoundStageRank = null,
        int? exactScoresRank = null,
        int? preRoundExactScoresRank = null)
    {
        const string sql = @"
            INSERT INTO [LeagueMemberStats]
            (
                [LeagueId],
                [UserId],
                [OverallRank],
                [MonthRank],
                [LiveRoundRank],
                [SnapshotOverallRank],
                [SnapshotMonthRank],
                [StableRoundRank],
                [StageRank],
                [PreRoundStageRank],
                [ExactScoresRank],
                [PreRoundExactScoresRank]
            )
            VALUES
            (
                @LeagueId,
                @UserId,
                @OverallRank,
                @MonthRank,
                @LiveRoundRank,
                @SnapshotOverallRank,
                @SnapshotMonthRank,
                @StableRoundRank,
                @StageRank,
                @PreRoundStageRank,
                @ExactScoresRank,
                @PreRoundExactScoresRank
            );";

        await ExecuteAsync(sql, new
        {
            LeagueId = leagueId,
            UserId = userId,
            OverallRank = overallRank,
            MonthRank = monthRank,
            LiveRoundRank = liveRoundRank,
            SnapshotOverallRank = snapshotOverallRank,
            SnapshotMonthRank = snapshotMonthRank,
            StableRoundRank = stableRoundRank,
            StageRank = stageRank,
            PreRoundStageRank = preRoundStageRank,
            ExactScoresRank = exactScoresRank,
            PreRoundExactScoresRank = preRoundExactScoresRank
        });
    }

    public async Task<int> AddEmailSettingsAsync(bool emailsEnabled)
    {
        const string sql = @"
            INSERT INTO [EmailSettings]
            (
                [EmailsEnabled]
            )
            VALUES
            (
                @EmailsEnabled
            );

            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new { EmailsEnabled = emailsEnabled });
    }

    public async Task AddLeaguePayoutAsync(int leagueId, string userId, decimal totalAmount, DateTime? paidAtUtc)
    {
        const string sql = @"
            INSERT INTO [LeaguePayouts]
            (
                [LeagueId],
                [UserId],
                [TotalAmount],
                [PaidAtUtc],
                [CreatedAtUtc],
                [UpdatedAtUtc]
            )
            VALUES
            (
                @LeagueId,
                @UserId,
                @TotalAmount,
                @PaidAtUtc,
                @CreatedAtUtc,
                @UpdatedAtUtc
            );";

        await ExecuteAsync(sql, new
        {
            LeagueId = leagueId,
            UserId = userId,
            TotalAmount = totalAmount,
            PaidAtUtc = paidAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task AddUserPayoutDetailsAsync(
        string userId,
        string? accountName,
        string? sortCode,
        string? accountNumber)
    {
        const string sql = @"
            INSERT INTO [UserPayoutDetails]
            (
                [UserId],
                [AccountName],
                [SortCode],
                [AccountNumber],
                [CreatedAtUtc],
                [UpdatedAtUtc]
            )
            VALUES
            (
                @UserId,
                @AccountName,
                @SortCode,
                @AccountNumber,
                @CreatedAtUtc,
                @UpdatedAtUtc
            );";

        await ExecuteAsync(sql, new
        {
            UserId = userId,
            AccountName = accountName,
            SortCode = sortCode,
            AccountNumber = accountNumber,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task<int> AddSeasonPassAsync(
        string userId,
        int seasonId,
        SeasonPassTier tier = SeasonPassTier.Standard,
        SeasonPassSource source = SeasonPassSource.Purchased)
    {
        // Tier and Source are stored as enum names, unlike LeaguePrizeSettings.PrizeType which stores the number.
        const string sql = @"
            INSERT INTO [SeasonPasses]
            (
                [UserId],
                [SeasonId],
                [Tier],
                [Source],
                [AmountPaid],
                [SmsFeePaid],
                [CreatedAtUtc]
            )
            VALUES
            (
                @UserId,
                @SeasonId,
                @Tier,
                @Source,
                @AmountPaid,
                @SmsFeePaid,
                @CreatedAtUtc
            );

            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            UserId = userId,
            SeasonId = seasonId,
            Tier = tier.ToString(),
            Source = source.ToString(),
            AmountPaid = 0m,
            SmsFeePaid = 0m,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task AddUserBadgeAsync(string userId, string badgeKey, DateTime awardedUtc, int? roundId = null)
    {
        // A null [RoundId] is how a lifetime badge is stored, and the unique index over (UserId, BadgeKey, RoundId,
        // SeasonId) treats nulls as equal - so a second award of the same badge needs a round to be scoped to. Nothing
        // on the read side looks at the column; it is the write path's idempotency key. See ITestDataSeeder.
        const string sql = @"
            INSERT INTO [UserBadges]
            (
                [UserId],
                [BadgeKey],
                [AwardedUtc],
                [RoundId]
            )
            VALUES
            (
                @UserId,
                @BadgeKey,
                @AwardedUtc,
                @RoundId
            );";

        await ExecuteAsync(sql, new { UserId = userId, BadgeKey = badgeKey, AwardedUtc = awardedUtc, RoundId = roundId });
    }

    public async Task<int> AddPricingSettingsAsync(decimal bufferRate, decimal minimumFloor)
    {
        const string sql = @"
            INSERT INTO [PricingSettings]
            (
                [BufferRate],
                [MinimumFloor]
            )
            VALUES
            (
                @BufferRate,
                @MinimumFloor
            );

            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new { BufferRate = bufferRate, MinimumFloor = minimumFloor });
    }

    public async Task<int> AddRunningCostAsync(
        string name,
        decimal amount,
        string frequency,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        string? notes)
    {
        const string sql = @"
            INSERT INTO [RunningCosts]
            (
                [Name],
                [Amount],
                [Frequency],
                [StartDateUtc],
                [EndDateUtc],
                [Notes],
                [CreatedAtUtc]
            )
            VALUES
            (
                @Name,
                @Amount,
                @Frequency,
                @StartDateUtc,
                @EndDateUtc,
                @Notes,
                @CreatedAtUtc
            );

            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            Name = name,
            Amount = amount,
            Frequency = frequency,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task<int> AddServiceFeeAsync(string provider, decimal percentFee, decimal fixedFee)
    {
        const string sql = @"
            INSERT INTO [ServiceFees]
            (
                [Provider],
                [PercentFee],
                [FixedFee]
            )
            VALUES
            (
                @Provider,
                @PercentFee,
                @FixedFee
            );

            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new { Provider = provider, PercentFee = percentFee, FixedFee = fixedFee });
    }

    public async Task DeleteMatchAsync(int matchId)
    {
        // No guard at all - the point is to show what the schema does on its own.
        await ExecuteAsync("DELETE FROM [Matches] WHERE [Id] = @MatchId;", new { MatchId = matchId });
    }

    private async Task ExecuteAsync(string sql, object parameters)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, parameters);
    }

    // Only used for SELECT CAST(SCOPE_IDENTITY() AS int) after an INSERT, which always returns a value.
    private async Task<T> ExecuteScalarAsync<T>(string sql, object parameters)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<T>(sql, parameters)
               ?? throw new InvalidOperationException($"Expected a scalar result from: {sql}");
    }
}
