using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Infrastructure.Repositories;

public class SeasonRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ISeasonRepository
{
    #region Create

    public async Task<Season> CreateAsync(Season season, CancellationToken cancellationToken)
    {
        const string sql = @"
                INSERT INTO [Seasons]
                (
                    [Name],
                    [StartDateUtc],
                    [EndDateUtc],
                    [IsActive],
                    [NumberOfRounds],
                    [CompetitionId],
                    [PassEntryPrice],
                    [PassSmsPrice]
                )
                VALUES
                (
                    @Name,
                    @StartDateUtc,
                    @EndDateUtc,
                    @IsActive,
                    @NumberOfRounds,
                    @CompetitionId,
                    @PassEntryPrice,
                    @PassSmsPrice
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: season,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        var newSeasonId = await Connection.ExecuteScalarAsync<int>(command);

        return new Season(
            id: newSeasonId,
            name: season.Name,
            startDateUtc: season.StartDateUtc,
            endDateUtc: season.EndDateUtc,
            isActive: season.IsActive,
            numberOfRounds: season.NumberOfRounds,
            competitionId: season.CompetitionId,
            passEntryPrice: season.PassEntryPrice,
            passSmsPrice: season.PassSmsPrice
        );
    }

    #endregion

    #region Read

    public async Task<Season?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM [Seasons] WHERE [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Id = id },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleOrDefaultAsync<Season>(command);
    }

    public async Task<IEnumerable<Season>> GetActiveSeasonsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM [Seasons] WHERE [IsActive] = 1;";

        var command = new CommandDefinition(
            commandText: sql,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<Season>(command);
    }

    public async Task<bool> HasPredictionsAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM [UserPredictions] up
                INNER JOIN [Matches] m ON up.[MatchId] = m.[Id]
                INNER JOIN [Rounds] r ON m.[RoundId] = r.[Id]
                WHERE r.[SeasonId] = @SeasonId
            ) THEN 1 ELSE 0 END;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { SeasonId = seasonId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<bool>(command);
    }

    #endregion

    #region Update

    public async Task UpdateAsync(Season season, CancellationToken cancellationToken)
    {
        const string sql = @"
                UPDATE [Seasons]
                SET
                    [Name] = @Name,
                    [StartDateUtc] = @StartDateUtc,
                    [EndDateUtc] = @EndDateUtc,
                    [IsActive] = @IsActive,
                    [NumberOfRounds] = @NumberOfRounds,
                    [CompetitionId] = @CompetitionId,
                    [PassEntryPrice] = @PassEntryPrice,
                    [PassSmsPrice] = @PassSmsPrice
                WHERE [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: season,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE ubu FROM [UserBoostUsages] ubu
                WHERE ubu.[SeasonId] = @SeasonId;
            DELETE ubu FROM [UserBoostUsages] ubu
                INNER JOIN [Rounds] r ON ubu.[RoundId] = r.[Id]
                WHERE r.[SeasonId] = @SeasonId;
            DELETE ubu FROM [UserBoostUsages] ubu
                INNER JOIN [Matches] m ON ubu.[MatchId] = m.[Id]
                INNER JOIN [Rounds] r ON m.[RoundId] = r.[Id]
                WHERE r.[SeasonId] = @SeasonId;

            DELETE w FROM [Winnings] w
                INNER JOIN [LeaguePrizeSettings] lps ON w.[LeaguePrizeSettingId] = lps.[Id]
                INNER JOIN [Leagues] l ON lps.[LeagueId] = l.[Id]
                WHERE l.[SeasonId] = @SeasonId;

            DELETE lms FROM [LeagueMemberStats] lms
                INNER JOIN [Leagues] l ON lms.[LeagueId] = l.[Id]
                WHERE l.[SeasonId] = @SeasonId;

            DELETE lrr FROM [LeagueRoundResults] lrr
                INNER JOIN [Rounds] r ON lrr.[RoundId] = r.[Id]
                WHERE r.[SeasonId] = @SeasonId;

            DELETE rr FROM [RoundResults] rr
                INNER JOIN [Rounds] r ON rr.[RoundId] = r.[Id]
                WHERE r.[SeasonId] = @SeasonId;

            DELETE FROM [Seasons] WHERE [Id] = @SeasonId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { SeasonId = seasonId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}
