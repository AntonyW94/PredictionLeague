using System.Security.Cryptography;

namespace PredictionLeague.Domain.Models;

public class PasswordResetToken
{
    public string Token { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Public constructor for loading from database (Dapper).
    /// </summary>
    public PasswordResetToken(string token, string userId, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        Token = token;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>
    /// Factory method to create a new password reset token.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the reset.</param>
    /// <param name="expiryHours">How long the token should be valid (default 1 hour).</param>
    public static PasswordResetToken Create(string userId, int expiryHours = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var now = DateTime.UtcNow;

        return new PasswordResetToken(
            token: token,
            userId: userId,
            createdAtUtc: now,
            expiresAtUtc: now.AddHours(expiryHours)
        );
    }

    /// <summary>
    /// Checks if the token has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc;
}
