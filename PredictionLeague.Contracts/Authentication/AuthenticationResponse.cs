namespace PredictionLeague.Contracts.Authentication;

public class AuthenticationResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Token { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? RefreshTokenForCookie { get; init; }
}