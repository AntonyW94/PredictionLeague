using MediatR;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.Login;

public class LoginCommand : IRequest<AuthenticationResponse>
{
    public string Email { get; }
    public string Password { get;}

    public LoginCommand(LoginRequest loginRequest)
    {
        Email = loginRequest.Email;
        Password = loginRequest.Password;
    }
}