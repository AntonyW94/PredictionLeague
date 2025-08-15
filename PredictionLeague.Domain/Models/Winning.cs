using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class Winning
{
    public int Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int LeaguePrizeSettingId { get; private set; }
    public decimal Amount { get; private set; } 
    public DateTime AwardedDate { get; private set; }
    public int? RoundNumber { get; private set; }
    public int? Month { get; private set; }

    private Winning() { }

    public static Winning Create(string userId, int leaguePrizeSettingId, decimal amount, int? roundNumber, int? month)
    {
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));
        Guard.Against.NegativeOrZero(leaguePrizeSettingId, nameof(leaguePrizeSettingId));
        Guard.Against.Negative(amount, nameof(amount));

        return new Winning
        {
            UserId = userId,
            LeaguePrizeSettingId = leaguePrizeSettingId,
            Amount = amount,
            AwardedDate = DateTime.UtcNow,
            RoundNumber = roundNumber,
            Month = month
        };
    }
}