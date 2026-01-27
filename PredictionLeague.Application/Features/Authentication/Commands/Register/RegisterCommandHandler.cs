using MediatR;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthenticationResponse>
{
    private readonly IUserManager _userManager;
    private readonly IAuthenticationTokenService _tokenService;

    public RegisterCommandHandler(IUserManager userManager, IAuthenticationTokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
            return new FailedAuthenticationResponse("Registration could not be completed. If you already have an account, please try logging in.");

        var newUser = ApplicationUser.Create(
            request.FirstName,
            request.LastName,
            request.Email
        );
        
        var result = await _userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
            throw new Common.Exceptions.IdentityUpdateException(result.Errors);

        await _userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

        var (accessToken, refreshToken, expiresAtUtc) = await _tokenService.GenerateTokensAsync(newUser, cancellationToken);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAtUtc: expiresAtUtc
        );
    }
}
