using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Web.Client.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResponse> RegisterAsync(RegisterRequest registerRequest);
    Task<AuthenticationResponse> LoginAsync(LoginRequest loginRequest);
    Task LogoutAsync();
    Task<bool> JoinPublicLeagueAsync(int leagueId);
}