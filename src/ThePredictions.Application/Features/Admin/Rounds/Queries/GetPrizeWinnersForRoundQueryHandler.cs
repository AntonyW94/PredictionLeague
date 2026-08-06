using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetPrizeWinnersForRoundQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetPrizeWinnersForRoundQuery, IReadOnlyList<PrizeWinner>>
{
    public async Task<IReadOnlyList<PrizeWinner>> Handle(GetPrizeWinnersForRoundQuery request, CancellationToken cancellationToken)
    {
        // Column order must match the PrizeWinnerRow constructor (Dapper maps positionally).
        // Every current Winning across the round's season is returned; the AlreadyNotified flag
        // (a LEFT JOIN against the PrizeNotifications sent-log) lets the send command skip prizes a
        // winner has already been emailed about. NULL round numbers / months are normalised to -1
        // in the join so the all-null overall/section prizes match correctly.
        const string sql = @"
            SELECT
                u.[Id] AS UserId,
                u.[Email],
                u.[FirstName],
                r.[DisplayName] AS RoundName,
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                lps.[Id] AS LeaguePrizeSettingId,
                lps.[PrizeType],
                lps.[PrizeDescription],
                lps.[Rank],
                lps.[Stage],
                w.[Amount],
                w.[RoundNumber],
                w.[Month],
                pr.[DisplayName] AS PrizeRoundName,
                CAST(CASE WHEN pn.[Id] IS NULL THEN 0 ELSE 1 END AS BIT) AS AlreadyNotified
            FROM
                [Rounds] r
            JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            JOIN
                [LeaguePrizeSettings] lps ON lps.[LeagueId] = l.[Id]
            JOIN
                [Winnings] w ON w.[LeaguePrizeSettingId] = lps.[Id]
            JOIN
                [AspNetUsers] u ON u.[Id] = w.[UserId]
            LEFT JOIN
                [Rounds] pr ON pr.[SeasonId] = r.[SeasonId]
                AND pr.[RoundNumber] = w.[RoundNumber]
            LEFT JOIN
                [PrizeNotifications] pn ON pn.[UserId] = w.[UserId]
                AND pn.[LeaguePrizeSettingId] = w.[LeaguePrizeSettingId]
                AND ISNULL(pn.[RoundNumber], -1) = ISNULL(w.[RoundNumber], -1)
                AND ISNULL(pn.[Month], -1) = ISNULL(w.[Month], -1)
            WHERE
                r.[Id] = @RoundId
                AND w.[Amount] > 0
            ORDER BY
                u.[Id],
                l.[Name]";

        var rows = await dbConnection.QueryAsync<PrizeWinnerRow>(
            sql,
            cancellationToken,
            new { RoundId = request.RoundId });

        return rows
            .GroupBy(row => row.UserId)
            .Select(group =>
            {
                var first = group.First();
                var prizes = group
                    .Select(row => new WonPrize(
                        row.LeagueId,
                        row.LeagueName,
                        row.LeaguePrizeSettingId,
                        row.PrizeType,
                        row.PrizeDescription,
                        row.Rank,
                        row.Stage,
                        row.Amount,
                        row.RoundNumber,
                        row.Month,
                        row.PrizeRoundName,
                        row.AlreadyNotified))
                    .ToList();

                return new PrizeWinner(
                    first.UserId,
                    first.Email,
                    first.FirstName,
                    first.RoundName,
                    prizes);
            })
            .ToList();
    }
}
