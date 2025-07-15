using MediatR;
using PredictionLeague.Contracts.Authentication;
using System.Security.Claims;

namespace PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;

public class LoginWithGoogleCommand : IRequest<AuthenticationResponse>
{
    public ClaimsPrincipal Principal { get; }
    public string LoginProvider { get; }
    public string ProviderKey { get; }

    public LoginWithGoogleCommand(ClaimsPrincipal principal, string loginProvider, string providerKey)
    {
        Principal = principal;
        LoginProvider = loginProvider;
        ProviderKey = providerKey;
    }
}
