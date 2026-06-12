using ThePredictions.Domain.Common;

namespace ThePredictions.Domain.Models;

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

    /// <summary>
    /// True when the token was revoked very recently (within <paramref name="graceWindow"/>)
    /// and has not expired. A token is normally revoked because it was rotated by a refresh;
    /// allowing it briefly afterwards lets a near-simultaneous sibling request (e.g. a second
    /// browser tab refreshing at the same moment) succeed instead of ending the session.
    /// </summary>
    public bool IsWithinReuseGrace(IDateTimeProvider dateTimeProvider, TimeSpan graceWindow) =>
        Revoked != null
        && !IsExpired(dateTimeProvider)
        && dateTimeProvider.UtcNow <= Revoked.Value.Add(graceWindow);

    public void Revoke(IDateTimeProvider dateTimeProvider)
    {
        Revoked = dateTimeProvider.UtcNow;
    }
}
