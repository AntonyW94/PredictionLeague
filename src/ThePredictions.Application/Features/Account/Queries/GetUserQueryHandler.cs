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

        return await dbConnection.QuerySingleOrDefaultAsync<UserDetails>(sql, cancellationToken, new { request.UserId });
    }
}