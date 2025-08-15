namespace PredictionLeague.Domain.Models;

public class RefreshToken
{
    public int Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTime Expires { get; init; }
    public bool IsExpired => DateTime.Now >= Expires;
    public DateTime Created { get; init; }
    public DateTime? Revoked { get; init; }
    public bool IsActive => Revoked == null && !IsExpired;
}
