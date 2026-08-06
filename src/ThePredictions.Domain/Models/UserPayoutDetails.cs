using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;

namespace ThePredictions.Domain.Models;

/// <summary>
/// A player's optional bank details for receiving peer-to-peer prize payouts.
/// The account fields hold ciphertext (encrypted at the command layer); the platform never moves money.
/// </summary>
public class UserPayoutDetails
{
    public string UserId { get; private set; } = string.Empty;
    public string? AccountName { get; private set; }
    public string? SortCode { get; private set; }
    public string? AccountNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public bool HasDetails => AccountName is not null && SortCode is not null && AccountNumber is not null;

    [ExcludeFromCodeCoverage(Justification = "Parameterless constructor for Dapper hydration: no logic to test.")]
    private UserPayoutDetails() { }

    public UserPayoutDetails(string userId, string? accountName, string? sortCode, string? accountNumber, DateTime createdAtUtc, DateTime updatedAtUtc)
    {
        UserId = userId;
        AccountName = accountName;
        SortCode = sortCode;
        AccountNumber = accountNumber;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static UserPayoutDetails Create(string userId, string? accountName, string? sortCode, string? accountNumber, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        var now = dateTimeProvider.UtcNow;

        return new UserPayoutDetails
        {
            UserId = userId,
            AccountName = accountName,
            SortCode = sortCode,
            AccountNumber = accountNumber,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string? accountName, string? sortCode, string? accountNumber, IDateTimeProvider dateTimeProvider)
    {
        AccountName = accountName;
        SortCode = sortCode;
        AccountNumber = accountNumber;
        UpdatedAtUtc = dateTimeProvider.UtcNow;
    }
}
