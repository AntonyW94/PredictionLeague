using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Domain.Models;

public class LeaguePrizeSetting
{
    public int Id { get; private set; }
    public int LeagueId { get; private set; }
    public PrizeType PrizeType { get; private set; }
    public int Rank { get; private set; }
    public decimal PrizeAmount { get; private set; }
    public string? PrizeDescription { get; private set; }

    private LeaguePrizeSetting() { }

    public static LeaguePrizeSetting Create(int leagueId, PrizeType prizeType, int rank, decimal prizeAmount, string? prizeDescription)
    {
        Guard.Against.NegativeOrZero(leagueId, nameof(leagueId));
        Guard.Against.NegativeOrZero(rank, nameof(rank));
        Guard.Against.Negative(prizeAmount, nameof(prizeAmount));

        return new LeaguePrizeSetting
        {
            LeagueId = leagueId,
            PrizeType = prizeType,
            Rank = rank,
            PrizeAmount = prizeAmount,
            PrizeDescription = prizeDescription,
        };
    }
}