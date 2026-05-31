using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public class GetAvailableSeasonPassesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetAvailableSeasonPassesQuery, IEnumerable<AvailableSeasonPassDto>>
{
    public async Task<IEnumerable<AvailableSeasonPassDto>> Handle(GetAvailableSeasonPassesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                CAST(CASE WHEN s.[PassStandardPrice] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS RequiresPayment,
                s.[PassStandardPrice] AS StandardPrice,
                s.[PassPremiumPrice] AS PremiumPrice,
                CAST(CASE WHEN (SELECT COUNT(*) FROM [SeasonPasses] WHERE [UserId] = @UserId) = 0 THEN 1 ELSE 0 END AS BIT) AS IsTrialEligible
            FROM
                [Seasons] s
            WHERE
                s.[IsActive] = 1
                AND NOT EXISTS (
                    SELECT 1
                    FROM [SeasonPasses] sp
                    WHERE sp.[UserId] = @UserId
                        AND sp.[SeasonId] = s.[Id]
                )
            ORDER BY
                s.[StartDateUtc] DESC;";

        return await dbConnection.QueryAsync<AvailableSeasonPassDto>(sql, cancellationToken, new { request.UserId });
    }
}
