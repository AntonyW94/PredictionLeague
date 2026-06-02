using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Infrastructure.Repositories;

public class WinningsRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IWinningsRepository
{
    public async Task<decimal> GetUserLeagueTotalAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                COALESCE(SUM(w.[Amount]), 0)
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
            WHERE
                lps.[LeagueId] = @LeagueId
                AND w.[UserId] = @UserId;";

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, UserId = userId }, transaction: Transaction, cancellationToken: cancellationToken);

        return await Connection.ExecuteScalarAsync<decimal>(command);
    }

    public async Task AddWinningsAsync(IEnumerable<Winning> winnings, CancellationToken cancellationToken)
    {
        if (!winnings.Any())
            return;

        const string sql = @"
            INSERT INTO [Winnings]
            (
                [UserId],
                [LeaguePrizeSettingId],
                [Amount],
                [AwardedDateUtc],
                [RoundNumber],
                [Month]
            )
            VALUES
            (
                @UserId,
                @LeaguePrizeSettingId,
                @Amount,
                @AwardedDateUtc,
                @RoundNumber,
                @Month
            );";

        var command = new CommandDefinition(commandText: sql, parameters: winnings, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteWinningsForRoundAsync(int leagueId, int roundNumber, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE
                w
            FROM
                [Winnings] w
            JOIN
                [LeaguePrizeSettings] lps ON w.[LeaguePrizeSettingId] = lps.[Id]
            WHERE
                lps.[LeagueId] = @LeagueId
                AND w.[RoundNumber] = @RoundNumber";

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, RoundNumber = roundNumber }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteWinningsForMonthAsync(int leagueId, int month, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE
                w
            FROM
                [Winnings] w
            JOIN
                [LeaguePrizeSettings] lps ON w.[LeaguePrizeSettingId] = lps.[Id]
            WHERE
                lps.[LeagueId] = @LeagueId
                AND w.[Month] = @Month";

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, Month = month }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteWinningsForOverallAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE
                w
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON w.[LeaguePrizeSettingId] = lps.[Id]
            WHERE
                lps.[LeagueId] = @leagueId
                AND lps.[PrizeType] = @PrizeType;";

        var command = new CommandDefinition(
            sql,
            new
            {
                LeagueId = leagueId,
                PrizeType = PrizeType.Overall
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteWinningsForMostExactScoresAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE
                w
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON w.[LeaguePrizeSettingId] = lps.[Id]
            WHERE
                lps.[LeagueId] = @LeagueId
                AND lps.[PrizeType] = @PrizeType;";

        var command = new CommandDefinition(
            sql,
            new
            {
                LeagueId = leagueId,
                PrizeType = PrizeType.MostExactScores
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteWinningsForSectionAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE
                w
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON w.[LeaguePrizeSettingId] = lps.[Id]
            WHERE
                lps.[LeagueId] = @LeagueId
                AND lps.[PrizeType] = @PrizeType;";

        var command = new CommandDefinition(
            sql,
            new
            {
                LeagueId = leagueId,
                PrizeType = PrizeType.Stages
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }
}
