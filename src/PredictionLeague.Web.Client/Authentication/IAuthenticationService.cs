using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Web.Client.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResponse> RegisterAsync(RegisterRequest registerRequest);
    Task<AuthenticationResponse> LoginAsync(LoginRequest loginRequest);
    Task LogoutAsync();
    Task<bool> RequestPasswordResetAsync(string email);
    Task<ResetPasswordResponse> ResetPasswordAsync(string token, string newPassword, string confirmPassword);
}