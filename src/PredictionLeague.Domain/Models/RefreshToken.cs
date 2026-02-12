namespace PredictionLeague.Domain.Models;

public class RefreshToken
{
    public int Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTime Expires { get; init; }
    public bool IsExpired => DateTime.UtcNow >= Expires;
    public DateTime Created { get; init; }
    public DateTime? Revoked { get; private set; }
    public bool IsActive => Revoked == null && !IsExpired;

    public RefreshToken() { }

    public RefreshToken(int id, string userId, string token, DateTime expires, DateTime created, DateTime? revoked)
    {
        Id = id;
        UserId = userId;
        Token = token;
        Expires = expires;
        Created = created;
        Revoked = revoked;
    }

    public void Revoke()
    {
        Revoked = DateTime.UtcNow;
    }
}
