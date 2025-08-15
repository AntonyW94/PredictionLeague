using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class Winnings
{
    public int Id { get; private set; }
    public int LeaguePrizeSettingId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int LeagueId { get; private set; }
    public decimal Amount { get; private set; } 
    public DateTime AwardedDate { get; private set; }
    public string? Reference { get; private set; }

    private Winnings() { }

    public static Winnings Create(int leaguePrizeSettingId, string userId, int leagueId, decimal amount, string? reference)
    {
        Guard.Against.NegativeOrZero(leaguePrizeSettingId, nameof(leaguePrizeSettingId));
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));
        Guard.Against.NegativeOrZero(leagueId, nameof(leagueId));
        Guard.Against.Negative(amount, nameof(amount));

        return new Winnings
        {
            LeaguePrizeSettingId = leaguePrizeSettingId,
            UserId = userId,
            LeagueId = leagueId,
            Amount = amount,
            Reference = reference,
            AwardedDate = DateTime.Now
        };
    }
}