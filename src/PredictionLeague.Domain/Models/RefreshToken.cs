namespace PredictionLeague.Domain.Models;

public class RefreshToken
{
    public int Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTime Expires { get; init; }
    public bool IsExpired => DateTime.UtcNow >= Expires;
    public DateTime Created { get; init; }
    public DateTime? Revoked { get; set; }
    public bool IsActive => Revoked == null && !IsExpired;

    public void Revoke()
    {
        Revoked = DateTime.UtcNow;
    }
}
