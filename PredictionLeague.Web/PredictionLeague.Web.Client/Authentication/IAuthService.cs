using PredictionLeague.Web.Client.Contracts.Auth;

namespace PredictionLeague.Web.Client.Authentication
{
    public interface IAuthService
    {
        Task<AuthResponse> Login(LoginRequest loginRequest);
        Task Logout();
        Task<RegisterResponse> Register(RegisterRequest registerRequest);
    }
}
