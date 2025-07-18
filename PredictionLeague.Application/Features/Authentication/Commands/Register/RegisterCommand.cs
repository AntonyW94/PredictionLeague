using MediatR;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.Register;

public class RegisterCommand : RegisterRequest, IRequest<RegisterResponse>
{

    public RegisterCommand(RegisterRequest request)
    {
        FirstName = request.FirstName;
        LastName = request.LastName;
        Email = request.Email;
        Password = request.Password;
    }
}