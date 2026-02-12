using Ardalis.GuardClauses;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class Winning
{
    public string UserId { get; private set; } = string.Empty;
    public int LeaguePrizeSettingId { get; private set; }
    public decimal Amount { get; private set; } 
    public DateTime AwardedDateUtc { get; private set; }
    public int? RoundNumber { get; private set; }
    public int? Month { get; private set; }

    private Winning() { }

    public static Winning Create(string userId, int leaguePrizeSettingId, decimal amount, int? roundNumber, int? month)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(leaguePrizeSettingId);
        Guard.Against.Negative(amount);

        return new Winning
        {
            UserId = userId,
            LeaguePrizeSettingId = leaguePrizeSettingId,
            Amount = amount,
            AwardedDateUtc = DateTime.UtcNow,
            RoundNumber = roundNumber,
            Month = month
        };
    }
}