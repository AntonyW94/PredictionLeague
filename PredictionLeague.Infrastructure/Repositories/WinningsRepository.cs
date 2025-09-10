using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class WinningsRepository : IWinningsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public WinningsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
                [AwardedDate],
                [RoundNumber],
                [Month]
            )
            VALUES
            (
                @UserId,
                @LeaguePrizeSettingId,
                @Amount,
                @AwardedDate,
                @RoundNumber,
                @Month
            );";

        var command = new CommandDefinition(commandText: sql, parameters: winnings, cancellationToken: cancellationToken);
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

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, RoundNumber = roundNumber }, cancellationToken: cancellationToken);
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

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, Month = month }, cancellationToken: cancellationToken);
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
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }
}