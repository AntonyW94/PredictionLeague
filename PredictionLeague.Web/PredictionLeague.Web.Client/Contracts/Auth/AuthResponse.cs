namespace PredictionLeague.Web.Client.Contracts.Auth;

public class AuthResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public string Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
}