using PredictionLeague.Contracts.Auth;

namespace PredictionLeague.Web.Client.Authentication;

public interface IAuthService
{
    Task<RegisterResponse> Register(RegisterRequest registerRequest);
    Task<AuthResponse> Login(LoginRequest loginRequest);
    Task Logout();
    Task<bool> JoinPublicLeagueAsync(int leagueId);
}