using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthenticationResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public RegisterCommandHandler(UserManager<ApplicationUser> userManager, IAuthenticationTokenService tokenService, IConfiguration configuration)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    public async Task<AuthenticationResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
            return new FailedAuthenticationResponse("User with this email already exists.");

        var newUser = new ApplicationUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email
        };

        var result = await _userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
            throw new Common.Exceptions.IdentityUpdateException(result.Errors);
        
        await _userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

        var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(newUser);
        var expiryMinutes = double.Parse(_configuration["JwtSettings:ExpiryMinutes"]!);
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        return new SuccessfulAuthenticationResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAt: expiresAt
        );
    }
}
