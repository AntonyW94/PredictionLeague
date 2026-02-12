using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Users;
using PredictionLeague.Domain.Common.Enumerations; 
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Admin.Users.Queries;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IEnumerable<UserDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetAllUsersQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id],
                u.[FirstName] + ' ' + u.[LastName] AS FullName,
                u.[Email],
                u.[PhoneNumber],
                CAST(CASE WHEN u.[PasswordHash] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS HasLocalPassword,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [AspNetUserRoles] ur WHERE ur.UserId = u.Id AND ur.RoleId = (SELECT Id FROM AspNetRoles WHERE Name = @AdminRoleName)) THEN 1 ELSE 0 END AS bit) AS IsAdmin,
                STRING_AGG(ul.[LoginProvider], ',') AS SocialProviders
            FROM
                [AspNetUsers] u
            LEFT JOIN
                [AspNetUserLogins] ul ON u.Id = ul.UserId
            GROUP BY
                u.[Id], u.[FirstName], u.[LastName], u.[Email], u.[PhoneNumber], u.[PasswordHash]
            ORDER BY
                FullName;";

        var parameters = new { AdminRoleName = nameof(ApplicationUserRole.Administrator) };
        var queryResult = await _dbConnection.QueryAsync<UserQueryResult>(sql, cancellationToken, parameters);

        return queryResult.Select(u => new UserDto(
            u.Id,
            u.FullName,
            u.Email,
            u.PhoneNumber,
            u.IsAdmin,
            u.HasLocalPassword,
            u.SocialProviders?.Split(',').ToList() ?? new List<string>()
        ));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UserQueryResult(
        string Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        bool HasLocalPassword,
        bool IsAdmin,
        string? SocialProviders
    );
}