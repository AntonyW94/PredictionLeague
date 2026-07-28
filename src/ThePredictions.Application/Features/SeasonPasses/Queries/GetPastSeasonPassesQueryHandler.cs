using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public class GetPastSeasonPassesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetPastSeasonPassesQuery, IEnumerable<PastSeasonPassDto>>
{
    public async Task<IEnumerable<PastSeasonPassDto>> Handle(GetPastSeasonPassesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                c.[LogoUrl] AS CompetitionLogoUrl,
                (
                    SELECT COUNT(DISTINCT lm.[UserId])
                    FROM [LeagueMembers] lm
                    INNER JOIN [Leagues] l2 ON l2.[Id] = lm.[LeagueId]
                    WHERE l2.[SeasonId] = s.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS PlayerCount
            FROM
                [Seasons] s
            JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                s.[IsActive] = 1
                AND NOT EXISTS (                                        -- not already held
                    SELECT 1
                    FROM [SeasonPasses] sp
                    WHERE sp.[UserId] = @UserId
                        AND sp.[SeasonId] = s.[Id]
                )
                AND EXISTS (                                            -- the season actually ran (had leagues)
                    SELECT 1
                    FROM [Leagues] l
                    WHERE l.[SeasonId] = s.[Id]
                )
                AND NOT EXISTS (                                        -- entry has closed everywhere - you can no longer join
                    SELECT 1
                    FROM [Leagues] l
                    WHERE l.[SeasonId] = s.[Id]
                        AND l.[EntryDeadlineUtc] > GETUTCDATE()
                )
            ORDER BY
                s.[StartDateUtc] DESC;";

        var passes = await dbConnection.QueryAsync<PastSeasonPassQueryResult>(
            sql,
            cancellationToken,
            new { request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return passes.Select(p => new PastSeasonPassDto(
            p.SeasonId,
            p.SeasonName,
            p.CompetitionLogoUrl,
            p.PlayerCount));
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PastSeasonPassQueryResult(
        int SeasonId,
        string SeasonName,
        string? CompetitionLogoUrl,
        int PlayerCount);
}
