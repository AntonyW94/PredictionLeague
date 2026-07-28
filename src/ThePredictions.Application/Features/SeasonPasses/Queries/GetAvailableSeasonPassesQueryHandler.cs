using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;

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
                c.[LogoUrl] AS CompetitionLogoUrl,
                CAST(CASE WHEN s.[PassStandardPrice] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS RequiresPayment,
                s.[PassStandardPrice] AS StandardPrice,
                s.[PassPremiumPrice] AS PremiumPrice,
                CAST(CASE WHEN (SELECT COUNT(*) FROM [SeasonPasses] WHERE [UserId] = @UserId) = 0 THEN 1 ELSE 0 END AS BIT) AS IsTrialEligible,
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
                s.[IsActive] = 1
                AND NOT EXISTS (
                    SELECT 1
                    FROM [SeasonPasses] sp
                    WHERE sp.[UserId] = @UserId
                        AND sp.[SeasonId] = s.[Id]
                )
                AND EXISTS (                                            -- only offer a pass while entry is still open (a league you could still join)
                    SELECT 1
                    FROM [Leagues] l
                    WHERE l.[SeasonId] = s.[Id]
                        AND l.[EntryDeadlineUtc] > GETUTCDATE()
                )
            ORDER BY
                s.[StartDateUtc] DESC;";

        var passes = await dbConnection.QueryAsync<AvailableSeasonPassQueryResult>(
            sql,
            cancellationToken,
            new { request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return passes.Select(p => new AvailableSeasonPassDto(
            p.SeasonId,
            p.SeasonName,
            p.CompetitionLogoUrl,
            p.RequiresPayment,
            p.StandardPrice,
            p.PremiumPrice,
            p.IsTrialEligible,
            p.PlayerCount,
            p.NextEntryDeadlineUtc));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record AvailableSeasonPassQueryResult(
        int SeasonId,
        string SeasonName,
        string? CompetitionLogoUrl,
        bool RequiresPayment,
        decimal? StandardPrice,
        decimal? PremiumPrice,
        bool IsTrialEligible,
        int PlayerCount,
        DateTime? NextEntryDeadlineUtc);
}
