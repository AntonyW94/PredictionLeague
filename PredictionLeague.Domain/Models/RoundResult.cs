using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RoundResult
{
    public int RoundId { get; }
    public string UserId { get; }
    public int TotalPoints { get; }

    public RoundResult(int roundId, string userId, int totalPoints)
    {
        RoundId = roundId;
        UserId = userId;
        TotalPoints = totalPoints;
    }
}