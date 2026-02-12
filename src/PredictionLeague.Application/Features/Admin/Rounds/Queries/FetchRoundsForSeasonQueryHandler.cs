using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class FetchRoundsForSeasonQueryHandler : IRequestHandler<FetchRoundsForSeasonQuery, IEnumerable<RoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public FetchRoundsForSeasonQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<RoundDto>> Handle(FetchRoundsForSeasonQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH ActiveMemberCount AS (
                SELECT
                    COUNT(DISTINCT lm.[UserId]) AS MemberCount
                FROM 
                    [LeagueMembers] lm
                JOIN 
                    [Leagues] l ON lm.[LeagueId] = l.[Id]
                WHERE 
                    l.[SeasonId] = @SeasonId 
                    AND lm.[Status] = @ApprovedStatus
            )

            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[ApiRoundName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                (SELECT COUNT(*) FROM [Matches] m WHERE m.[RoundId] = r.[Id]) as MatchCount
            FROM
                [Rounds] r
            CROSS JOIN 
                [ActiveMemberCount] amc
            WHERE
                r.[SeasonId] = @SeasonId
            ORDER BY
                r.[RoundNumber];";

        var queryResult = await _dbConnection.QueryAsync<RoundQueryResult>(sql, cancellationToken, new { request.SeasonId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return queryResult.Select(r => new RoundDto(
            r.Id,
            r.SeasonId,
            r.RoundNumber,
            r.ApiRoundName,
            r.StartDateUtc,
            r.DeadlineUtc,
            Enum.Parse<RoundStatus>(r.Status),
            r.MatchCount
        )).ToList();
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record RoundQueryResult(
        int Id,
        int SeasonId,
        int RoundNumber,
        string ApiRoundName,
        DateTime StartDateUtc,
        DateTime DeadlineUtc,
        string Status,
        int MatchCount
    );
}