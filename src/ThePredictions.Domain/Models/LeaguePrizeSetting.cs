using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class LeaguePrizeSetting
{
    public int Id { get; init; }
    public int LeagueId { get; private set; }
    public PrizeType PrizeType { get; private set; }
    public int Rank { get; private set; }
    public decimal PrizeAmount { get; private set; }

    /// <summary>Optional display label (e.g. "1st", "Per round", "Group stage - 1st").</summary>
    public string? PrizeDescription { get; private set; }

    /// <summary>The tournament stage this prize belongs to (Section prizes only); otherwise null.</summary>
    public string? Stage { get; private set; }

    private LeaguePrizeSetting() { }

    public static LeaguePrizeSetting Create(int leagueId, PrizeType prizeType, int rank, decimal prizeAmount, string? stage = null, string? prizeDescription = null)
    {
        Guard.Against.NegativeOrZero(leagueId);
        Guard.Against.NegativeOrZero(rank);
        Guard.Against.Negative(prizeAmount);

        return new LeaguePrizeSetting
        {
            LeagueId = leagueId,
            PrizeType = prizeType,
            Rank = rank,
            PrizeAmount = prizeAmount,
            Stage = stage,
            PrizeDescription = prizeDescription
        };
    }
}