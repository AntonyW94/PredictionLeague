using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;

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
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [SeasonPasses] sp WHERE sp.[UserId] = @UserId AND sp.[SeasonId] = s.[Id]) THEN 1 ELSE 0 END AS BIT) AS AlreadyHeld,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [Leagues] l WHERE l.[SeasonId] = s.[Id] AND l.[EntryDeadlineUtc] > GETUTCDATE()) THEN 1 ELSE 0 END AS BIT) AS EntryOpen,
                (
                    SELECT COUNT(DISTINCT lm.[UserId])
                    FROM [LeagueMembers] lm
                    INNER JOIN [Leagues] l2 ON l2.[Id] = lm.[LeagueId]
                    WHERE l2.[SeasonId] = s.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS PlayerCount,
                (
                    SELECT MIN(l3.[EntryDeadlineUtc])
                    FROM [Leagues] l3
                    WHERE l3.[SeasonId] = s.[Id]
                        AND l3.[EntryDeadlineUtc] > GETUTCDATE()
                ) AS NextEntryDeadlineUtc
            FROM
                [Seasons] s
            JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                s.[Id] = @SeasonId;";

        return await dbConnection.QuerySingleOrDefaultAsync<SeasonPassOptionsDto>(
            sql,
            cancellationToken,
            new { request.UserId, request.SeasonId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }
}
