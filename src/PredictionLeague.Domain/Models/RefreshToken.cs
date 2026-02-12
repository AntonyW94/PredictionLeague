using PredictionLeague.Domain.Common;

namespace PredictionLeague.Domain.Models;

public class RefreshToken
{
    public int Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTime Expires { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Revoked { get; private set; }

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

    public bool IsExpired(IDateTimeProvider dateTimeProvider) => dateTimeProvider.UtcNow >= Expires;

    public bool IsActive(IDateTimeProvider dateTimeProvider) => Revoked == null && !IsExpired(dateTimeProvider);

    public void Revoke(IDateTimeProvider dateTimeProvider)
    {
        Revoked = dateTimeProvider.UtcNow;
    }
}
