using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public class GetSeasonPassOptionsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetSeasonPassOptionsQuery, SeasonPassOptionsDto?>
{
    public async Task<SeasonPassOptionsDto?> Handle(GetSeasonPassOptionsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                c.[LogoUrl] AS CompetitionLogoUrl,
                c.[Description] AS CompetitionDescription,
                CAST(CASE WHEN s.[PassStandardPrice] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS RequiresPayment,
                s.[PassStandardPrice] AS StandardPrice,
                s.[PassPremiumPrice] AS PremiumPrice,
                CAST(CASE WHEN (SELECT COUNT(*) FROM [SeasonPasses] WHERE [UserId] = @UserId) = 0 THEN 1 ELSE 0 END AS BIT) AS IsTrialEligible,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [SeasonPasses] sp WHERE sp.[UserId] = @UserId AND sp.[SeasonId] = s.[Id]) THEN 1 ELSE 0 END AS BIT) AS AlreadyHeld
            FROM
                [Seasons] s
            JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                s.[Id] = @SeasonId;";

        return await dbConnection.QuerySingleOrDefaultAsync<SeasonPassOptionsDto>(sql, cancellationToken, new { request.UserId, request.SeasonId });
    }
}
