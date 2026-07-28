using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

public class GetAllUsersQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetAllUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id],
                u.[FirstName] + ' ' + u.[LastName] AS FullName,
                u.[Email],
                u.[PhoneNumber],
                u.[EmailConfirmed],
                CAST(CASE WHEN u.[PasswordHash] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS HasLocalPassword,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [AspNetUserRoles] ur WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = (SELECT r.[Id] FROM [AspNetRoles] r WHERE r.[Name] = @AdminRoleName)) THEN 1 ELSE 0 END AS bit) AS IsAdmin,
                STRING_AGG(ul.[LoginProvider], ',') AS SocialProviders,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [SeasonPasses] sp WHERE sp.[UserId] = u.[Id]) THEN 1 ELSE 0 END AS bit) AS HasSeasonPass,
                (
                    SELECT
                        COUNT(1)
                    FROM
                        [Leagues] l
                    WHERE
                        l.[AdministratorUserId] = u.[Id]
                ) AS LeaguesCreated,
                (
                    SELECT
                        COUNT(1)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[UserId] = u.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS LeaguesJoinedApproved,
                (
                    SELECT
                        COUNT(1)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[UserId] = u.[Id]
                        AND lm.[Status] = @PendingStatus
                ) AS LeaguesJoinedPending,
                (
                    SELECT
                        COALESCE(SUM(w.[Amount]), 0)
                    FROM
                        [Winnings] w
                    WHERE
                        w.[UserId] = u.[Id]
                ) AS TotalWinnings,
                (
                    SELECT
                        COALESCE(SUM(sp.[AmountPaid] + sp.[SmsFeePaid]), 0)
                    FROM
                        [SeasonPasses] sp
                    WHERE
                        sp.[UserId] = u.[Id]
                        AND sp.[Source] = @PurchasedSource
                ) AS SeasonPassSpend,
                (
                    SELECT
                        COALESCE(SUM(l.[Price]), 0)
                    FROM
                        [LeagueMembers] lm
                    INNER JOIN
                        [Leagues] l ON l.[Id] = lm.[LeagueId]
                    WHERE
                        lm.[UserId] = u.[Id]
                        AND lm.[Status] = @ApprovedStatus
                        AND l.[IsFree] = 0
                        AND l.[Price] > 0
                ) AS LeagueEntrySpend
            FROM
                [AspNetUsers] u
            LEFT JOIN
                [AspNetUserLogins] ul ON u.[Id] = ul.[UserId]
            GROUP BY
                u.[Id], u.[FirstName], u.[LastName], u.[Email], u.[PhoneNumber], u.[PasswordHash], u.[EmailConfirmed]
            ORDER BY
                FullName;";

        var parameters = new
        {
            AdminRoleName = nameof(ApplicationUserRole.Administrator),
            ApprovedStatus = nameof(LeagueMemberStatus.Approved),
            PendingStatus = nameof(LeagueMemberStatus.Pending),
            PurchasedSource = nameof(SeasonPassSource.Purchased)
        };

        var queryResult = await dbConnection.QueryAsync<UserQueryResult>(sql, cancellationToken, parameters);

        return queryResult.Select(u => new UserDto(
            u.Id,
            u.FullName,
            u.Email,
            u.PhoneNumber,
            u.IsAdmin,
            u.HasLocalPassword,
            u.SocialProviders?.Split(',').ToList() ?? new List<string>(),
            u.EmailConfirmed,
            u.HasSeasonPass,
            u.LeaguesCreated,
            u.LeaguesJoinedApproved,
            u.LeaguesJoinedPending,
            u.TotalWinnings,
            u.SeasonPassSpend,
            u.LeagueEntrySpend
        ));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UserQueryResult(
        string Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        bool EmailConfirmed,
        bool HasLocalPassword,
        bool IsAdmin,
        string? SocialProviders,
        bool HasSeasonPass,
        int LeaguesCreated,
        int LeaguesJoinedApproved,
        int LeaguesJoinedPending,
        decimal TotalWinnings,
        decimal SeasonPassSpend,
        decimal LeagueEntrySpend
    );
}
