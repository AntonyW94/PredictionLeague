using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeagueDashboardQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetLeagueDashboardQuery, LeagueDashboardDto?>
{
    public async Task<LeagueDashboardDto?> Handle(GetLeagueDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!request.IsAdmin)
        {
            const string authSql = @"
                SELECT COUNT(1) FROM [LeagueMembers] 
                WHERE [LeagueId] = @LeagueId AND [UserId] = @UserId AND [Status] = @ApprovedStatus;";

            var isMember = await dbConnection.QuerySingleOrDefaultAsync<bool>(authSql, cancellationToken, new
            {
                request.LeagueId,
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            });

            if (!isMember)
                return null;
        }
        
        const string leagueSql = @"
            SELECT
                l.[Name],
                c.[Type] AS CompetitionType,
                s.[StartDateUtc],
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus) AS MemberCount,
                (l.[Price] * (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus) + ISNULL(l.[PrizeFundOverride], 0)) AS TotalPrizeFund,
                l.[IsFree],
                CAST(CASE
                    WHEN (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = @CompletedStatus) >= s.[NumberOfRounds]
                    THEN 1
                    ELSE 0
                END AS bit) AS IsFinished
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN
                [Competitions] c ON s.[CompetitionId] = c.[Id]
            WHERE
                l.[Id] = @LeagueId";

        var leagueInfo = await dbConnection.QuerySingleOrDefaultAsync<(string Name, int CompetitionType, DateTime StartDateUtc, int MemberCount, decimal TotalPrizeFund, bool IsFree, bool IsFinished)>(
            leagueSql, cancellationToken, new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed)
            });
        if (leagueInfo == default)
            return null;

        const string roundsSql = @"
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
            JOIN
                [Leagues] l ON r.[SeasonId] = l.[SeasonId]
            WHERE
                l.[Id] = @LeagueId
            ORDER BY
                r.[RoundNumber] DESC;";

        var parameters = new
        {
            request.LeagueId
        };
        var rounds = await dbConnection.QueryAsync<RoundDto>(roundsSql, cancellationToken, parameters);

        const string membersSql = @"
            SELECT
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS FullName,
                lm.[Status],
                lm.[JoinedAtUtc]
            FROM
                [LeagueMembers] lm
            JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] IN (@ApprovedStatus, @PendingStatus)
            ORDER BY
                lm.[JoinedAtUtc]";

        var members = await dbConnection.QueryAsync<LeagueDashboardMemberDto>(
            membersSql, cancellationToken, new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                PendingStatus = nameof(LeagueMemberStatus.Pending)
            });

        return new LeagueDashboardDto
        {
            LeagueName = leagueInfo.Name,
            CompetitionType = leagueInfo.CompetitionType,
            SeasonStartDateUtc = leagueInfo.StartDateUtc,
            MemberCount = leagueInfo.MemberCount,
            TotalPrizeFund = leagueInfo.TotalPrizeFund,
            IsFinished = leagueInfo.IsFinished,
            IsFree = leagueInfo.IsFree,
            Members = members.ToList(),
            ViewableRounds = rounds.ToList()
        };
    }
}