using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public class GetMySeasonPassesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetMySeasonPassesQuery, IEnumerable<MySeasonPassDto>>
{
    public async Task<IEnumerable<MySeasonPassDto>> Handle(GetMySeasonPassesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sp.[SeasonId],
                s.[Name] AS SeasonName,
                c.[LogoUrl] AS CompetitionLogoUrl,
                sp.[Tier],
                sp.[Source],
                sp.[AmountPaid],
                CAST(CASE WHEN sp.[Tier] = @PremiumTier THEN 1 ELSE 0 END AS BIT) AS HasSmsReminders,
                sp.[CreatedAtUtc]
            FROM
                [SeasonPasses] sp
            JOIN
                [Seasons] s ON s.[Id] = sp.[SeasonId]
            JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                sp.[UserId] = @UserId
            ORDER BY
                sp.[CreatedAtUtc] DESC;";

        return await dbConnection.QueryAsync<MySeasonPassDto>(sql, cancellationToken, new { request.UserId, PremiumTier = nameof(SeasonPassTier.Premium) });
    }
}
