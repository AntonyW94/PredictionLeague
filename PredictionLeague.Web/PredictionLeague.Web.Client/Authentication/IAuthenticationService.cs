using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Web.Client.Authentication;

public interface IAuthenticationService
{
    Task<RegisterResponse> Register(RegisterRequest registerRequest);
    Task<AuthenticationResponse> Login(LoginRequest loginRequest);
    Task Logout();
    Task<bool> JoinPublicLeagueAsync(int leagueId);
}