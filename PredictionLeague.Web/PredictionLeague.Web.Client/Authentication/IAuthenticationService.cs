using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Web.Client.Authentication;

public interface IAuthenticationService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest registerRequest);
    Task<AuthenticationResponse> LoginAsync(LoginRequest loginRequest);
    Task LogoutAsync();
    Task<bool> JoinPublicLeagueAsync(int leagueId);
}