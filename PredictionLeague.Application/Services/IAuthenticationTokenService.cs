using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Services
{
    public interface IAuthenticationTokenService
    {
        Task<AuthenticationResponse> GenerateAccessToken(ApplicationUser user);
        Task<RefreshToken> GenerateAndStoreRefreshToken(ApplicationUser user);
    }
}
