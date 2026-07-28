using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

public class GetPendingMembersForAdminQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetPendingMembersForAdminQuery, PendingMembersResultDto>
{
    public async Task<PendingMembersResultDto> Handle(
        GetPendingMembersForAdminQuery request,
        CancellationToken cancellationToken)
    {
        const string leaguesSql = @"
            SELECT
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                l.[EntryDeadlineUtc],
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus) AS MemberCount,
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @PendingStatus) AS PendingCount,
                l.[Price],
                l.[IsFree],
                l.[EntryCode]
            FROM
                [Leagues] l
            WHERE
                l.[AdministratorUserId] = @UserId
                AND l.[EntryDeadlineUtc] >= GETUTCDATE()
            ORDER BY
                l.[Name]";

        var adminLeagues = (await dbConnection.QueryAsync<AdminLeagueSummaryQueryResult>(
                leaguesSql,
                cancellationToken,
                new
                {
                    request.UserId,
                    ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                    PendingStatus = nameof(LeagueMemberStatus.Pending)
                }))
            .Select(l => new AdminLeagueSummaryDto(
                l.LeagueId,
                l.LeagueName,
                l.EntryDeadlineUtc,
                l.MemberCount,
                l.PendingCount,
                l.Price,
                l.IsFree,
                l.EntryCode))
            .ToList();

        if (!adminLeagues.Any())
        {
            return new PendingMembersResultDto
            {
                IsAdminOfOpenLeague = false
            };
        }

        const string membersSql = @"
            SELECT
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                lm.[UserId],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS MemberName,
                lm.[JoinedAtUtc]
            FROM
                [LeagueMembers] lm
            JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE
                l.[AdministratorUserId] = @UserId
                AND l.[EntryDeadlineUtc] >= GETUTCDATE()
                AND lm.[Status] = @PendingStatus
            ORDER BY
                l.[Name],
                lm.[JoinedAtUtc]";

        var members = await dbConnection.QueryAsync<PendingLeagueMemberQueryResult>(
            membersSql,
            cancellationToken,
            new
            {
                request.UserId,
                PendingStatus = nameof(LeagueMemberStatus.Pending)
            });

        return new PendingMembersResultDto
        {
            IsAdminOfOpenLeague = true,
            AdminLeagues = adminLeagues,
            Members = members
                .Select(m => new PendingLeagueMemberDto(
                    m.LeagueId,
                    m.LeagueName,
                    m.UserId,
                    m.MemberName,
                    m.JoinedAtUtc))
                .ToList()
        };
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record AdminLeagueSummaryQueryResult(
        int LeagueId,
        string LeagueName,
        DateTime EntryDeadlineUtc,
        int MemberCount,
        int PendingCount,
        decimal Price,
        bool IsFree,
        string? EntryCode);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PendingLeagueMemberQueryResult(
        int LeagueId,
        string LeagueName,
        string UserId,
        string MemberName,
        DateTime JoinedAtUtc);
}
