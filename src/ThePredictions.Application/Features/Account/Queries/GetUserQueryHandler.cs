using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Account;

namespace ThePredictions.Application.Features.Account.Queries;

public class GetUserQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetUserQuery, UserDetails?>
{
    public async Task<UserDetails?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [FirstName],
                [LastName],
                [Email],
                [PhoneNumber],
                [PreferredTheme],
                CAST(CASE WHEN [MarketingOptInAtUtc] IS NOT NULL THEN 1 ELSE 0 END AS bit) AS MarketingOptIn
            FROM [AspNetUsers]
            WHERE [Id] = @UserId;";

        var user = await dbConnection.QuerySingleOrDefaultAsync<UserQueryResult>(sql, cancellationToken, new { request.UserId });

        return user is null
            ? null
            : new UserDetails(
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.PreferredTheme,
                user.MarketingOptIn);
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UserQueryResult(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string PreferredTheme,
        bool MarketingOptIn);
}
