using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class LeaguePrizeSetting
{
    public int Id { get; init; }
    public int LeagueId { get; private set; }
    public PrizeType PrizeType { get; private set; }
    public int Rank { get; private set; }
    public decimal PrizeAmount { get; private set; }

    private LeaguePrizeSetting() { }

    public static LeaguePrizeSetting Create(int leagueId, PrizeType prizeType, int rank, decimal prizeAmount)
    {
        Guard.Against.NegativeOrZero(leagueId);
        Guard.Against.NegativeOrZero(rank);
        Guard.Against.Negative(prizeAmount);

        return new LeaguePrizeSetting
        {
            LeagueId = leagueId,
            PrizeType = prizeType,
            Rank = rank,
            PrizeAmount = prizeAmount
        };
    }
}