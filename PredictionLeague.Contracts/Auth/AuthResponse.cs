namespace PredictionLeague.Contracts.Auth;

public class AuthResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Token { get; init; }
    public DateTime? ExpiresAt { get; init; }
}