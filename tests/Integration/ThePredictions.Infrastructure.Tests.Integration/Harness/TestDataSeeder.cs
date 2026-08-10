using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Infrastructure.Tests.Integration.Harness;

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
internal sealed class TestDataSeeder(IDbConnectionFactory connectionFactory)
{
    private const string DefaultTheme = "light";

    /// <summary>
    /// The shared backdrop nearly every test needs: a competition, an active season, two teams to
    /// play each other, and one player.
    /// </summary>
    internal async Task<SeededBackdrop> AddBackdropAsync()
    {
        var competitionId = await AddCompetitionAsync();
        var seasonId = await AddSeasonAsync(competitionId);
        var homeTeamId = await AddTeamAsync("Arsenal", "ARS");
        var awayTeamId = await AddTeamAsync("Chelsea", "CHE");
        var userId = await AddUserAsync("Ada", "Lovelace");

        return new SeededBackdrop(competitionId, seasonId, homeTeamId, awayTeamId, userId);
    }

    internal async Task<int> AddCompetitionAsync(string code = "TEST")
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

    internal async Task<int> AddSeasonAsync(int competitionId, string name = "2026/27", int numberOfRounds = 38)
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
            IsActive = true,
            NumberOfRounds = numberOfRounds,
            StartDateUtc = DateTime.UtcNow.AddMonths(-1),
            EndDateUtc = DateTime.UtcNow.AddMonths(9),
            CompetitionId = competitionId
        });
    }

    internal async Task<int> AddTeamAsync(string name, string abbreviation)
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

    internal async Task<string> AddUserAsync(string firstName, string lastName)
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

    internal async Task<int> AddRoundAsync(
        int seasonId,
        int roundNumber,
        DateTime deadlineUtc,
        RoundStatus status = RoundStatus.Published,
        DateTime? startDateUtc = null)
    {
        const string sql = @"
            INSERT INTO [Rounds]
            (
                [SeasonId],
                [RoundNumber],
                [DisplayName],
                [Status],
                [StartDateUtc],
                [DeadlineUtc]
            )
            VALUES
            (
                @SeasonId,
                @RoundNumber,
                @DisplayName,
                @Status,
                @StartDateUtc,
                @DeadlineUtc
            );
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await ExecuteScalarAsync<int>(sql, new
        {
            SeasonId = seasonId,
            RoundNumber = roundNumber,
            DisplayName = $"Round {roundNumber}",
            Status = status.ToString(),
            StartDateUtc = startDateUtc ?? deadlineUtc.AddDays(-1),
            DeadlineUtc = deadlineUtc
        });
    }

    internal async Task<int> AddMatchAsync(
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

    internal async Task AddPredictionAsync(int matchId, string userId, int homeScore = 2, int awayScore = 1)
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
            Outcome = (int)PredictionOutcome.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });
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
