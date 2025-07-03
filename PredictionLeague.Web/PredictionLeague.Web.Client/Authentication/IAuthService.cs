using PredictionLeague.Shared.Auth;

namespace PredictionLeague.Web.Client.Authentication
{
    public interface IAuthService
    {
        Task<AuthResponse> Login(LoginRequest loginRequest);
        Task Logout();
        Task<RegisterResponse> Register(RegisterRequest registerRequest);
        Task<bool> JoinPublicLeagueAsync(int leagueId);
    }
}
