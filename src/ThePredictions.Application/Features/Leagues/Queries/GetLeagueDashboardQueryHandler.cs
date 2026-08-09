using ThePredictions.Domain.Common.Exceptions;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetLeagueDashboardQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetLeagueDashboardQuery, LeagueDashboardDto>
{
    public async Task<LeagueDashboardDto> Handle(GetLeagueDashboardQuery request, CancellationToken cancellationToken)
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

            // Deliberately "not found" rather than "not allowed": a non-member must not be able to
            // learn that a league exists by the status code they get back, so both cases answer 404
            // with the same message. This is not a mistaken use of EntityNotFoundException - the
            // league may well exist.
            if (!isMember)
                throw new EntityNotFoundException("League", request.LeagueId);
        }
        
        const string leagueSql = @"
            SELECT
                l.[Name],
                c.[Type] AS CompetitionType,
                s.[StartDateUtc],
                l.[EntryDeadlineUtc],
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

        var leagueInfo = await dbConnection.QuerySingleOrDefaultAsync<(string Name, int CompetitionType, DateTime StartDateUtc, DateTime? EntryDeadlineUtc, int MemberCount, decimal TotalPrizeFund, bool IsFree, bool IsFinished)>(
            leagueSql, cancellationToken, new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed)
            });
        if (leagueInfo == default)
            throw new EntityNotFoundException("League", request.LeagueId);

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
        var rounds = await dbConnection.QueryAsync<RoundQueryResult>(roundsSql, cancellationToken, parameters);

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
                u.[FirstName],
                u.[LastName]";

        var members = await dbConnection.QueryAsync<LeagueDashboardMemberQueryResult>(
            membersSql, cancellationToken, new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                PendingStatus = nameof(LeagueMemberStatus.Pending)
            });

        return new LeagueDashboardDto
        {
            LeagueName = leagueInfo.Name,
            CompetitionType = (CompetitionType)leagueInfo.CompetitionType,
            SeasonStartDateUtc = leagueInfo.StartDateUtc,
            EntryDeadlineUtc = leagueInfo.EntryDeadlineUtc,
            MemberCount = leagueInfo.MemberCount,
            TotalPrizeFund = leagueInfo.TotalPrizeFund,
            IsFinished = leagueInfo.IsFinished,
            IsFree = leagueInfo.IsFree,
            Members = members
                .Select(m => new LeagueDashboardMemberDto(
                    m.FullName,
                    m.Status,
                    m.JoinedAtUtc))
                .ToList(),
            ViewableRounds = rounds
                .Select(r => new RoundDto(
                    r.Id,
                    r.SeasonId,
                    r.RoundNumber,
                    r.ApiRoundName,
                    r.StartDateUtc,
                    r.DeadlineUtc,
                    r.Status,
                    r.MatchCount))
                .ToList()
        };
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record RoundQueryResult(
        int Id,
        int SeasonId,
        int RoundNumber,
        string? ApiRoundName,
        DateTime StartDateUtc,
        DateTime DeadlineUtc,
        RoundStatus Status,
        int MatchCount);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record LeagueDashboardMemberQueryResult(
        string FullName,
        string Status,
        DateTime JoinedAtUtc);
}
