using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Account;

namespace PredictionLeague.Application.Features.Account.Queries;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDetails?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetUserQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<UserDetails?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [FirstName],
                [LastName],
                [Email],
                [PhoneNumber]
            FROM [dbo].[AspNetUsers]
            WHERE [Id] = @UserId;";

        return await _dbConnection.QuerySingleOrDefaultAsync<UserDetails>(sql, cancellationToken, new { request.UserId });
    }
}