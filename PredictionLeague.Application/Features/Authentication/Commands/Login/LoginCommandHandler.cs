using MediatR;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthenticationResponse>
{
    private readonly IUserManager _userManager;
    private readonly IAuthenticationTokenService _tokenService;

    public LoginCommandHandler(IUserManager userManager, IAuthenticationTokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return new FailedAuthenticationResponse("Invalid email or password.");

        var (accessToken, refreshToken, expiresAt) = await _tokenService.GenerateTokensAsync(user, cancellationToken);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAt: expiresAt
        );
    }
}