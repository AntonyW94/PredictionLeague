using MediatR;
using PredictionLeague.Contracts.Account;

namespace PredictionLeague.Application.Features.Account.Queries;

public class GetUserQuery : IRequest<UserDetails?>
{
    public string UserId { get; }

    public GetUserQuery(string userId)
    {
        UserId = userId;
    }
}