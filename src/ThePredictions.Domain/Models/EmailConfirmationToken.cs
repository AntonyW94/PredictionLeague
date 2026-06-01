using ThePredictions.Domain.Common;

namespace ThePredictions.Domain.Models;

public class EmailConfirmationToken
{
    public string Token { get; private set; }
    public string UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; }

    /// <summary>
    /// Public constructor for loading from database (Dapper).
    /// </summary>
    public EmailConfirmationToken(string token, string userId, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        Token = token;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>
    /// Factory method to create a new email confirmation token.
    /// </summary>
    /// <param name="token">The generated token string (caller is responsible for generation).</param>
    /// <param name="userId">The ID of the user confirming their email.</param>
    /// <param name="dateTimeProvider">Provides the current UTC time.</param>
    /// <param name="expiryHours">How long the token should be valid (default 72 hours - links sit in inboxes).</param>
    public static EmailConfirmationToken Create(string token, string userId, IDateTimeProvider dateTimeProvider, int expiryHours = 72)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var now = dateTimeProvider.UtcNow;

        return new EmailConfirmationToken(
            token: token,
            userId: userId,
            createdAtUtc: now,
            expiresAtUtc: now.AddHours(expiryHours)
        );
    }

    /// <summary>
    /// Checks if the token has expired.
    /// </summary>
    public bool IsExpired(IDateTimeProvider dateTimeProvider) => dateTimeProvider.UtcNow > ExpiresAtUtc;
}
