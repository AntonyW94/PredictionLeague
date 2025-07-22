using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Services;

public interface IAuthenticationTokenService
{
    Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(ApplicationUser user);

    //Task<AuthenticationResponse> GenerateAccessToken(ApplicationUser user);
    //Task<RefreshToken> GenerateAndStoreRefreshToken(ApplicationUser user);
}