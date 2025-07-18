using MediatR;
using PredictionLeague.Contracts.Account;

namespace PredictionLeague.Application.Features.Account.Commands;

public class UpdateUserDetailsCommand : UpdateUserDetailsRequest, IRequest
{
    public string UserId { get; }

    public UpdateUserDetailsCommand(UpdateUserDetailsRequest request, string userId)
    {
        FirstName = request.FirstName;
        LastName = request.LastName;
        PhoneNumber = request.PhoneNumber;
        UserId = userId;
    }
}