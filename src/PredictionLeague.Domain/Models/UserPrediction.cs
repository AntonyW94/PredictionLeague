using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class UserPrediction
{
    public int Id { get; init; }
    public int MatchId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int PredictedHomeScore { get; init; }
    public int PredictedAwayScore { get; init; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public PredictionOutcome Outcome { get; private set; } = PredictionOutcome.Pending;

    private UserPrediction() { }

    public static UserPrediction Create(string userId, int matchId, int homeScore, int awayScore, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(matchId);
        Guard.Against.Negative(homeScore);
        Guard.Against.Negative(awayScore);

        var nowUtc = dateTimeProvider.UtcNow;

        return new UserPrediction
        {
            UserId = userId,
            MatchId = matchId,
            PredictedHomeScore = homeScore,
            PredictedAwayScore = awayScore,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Outcome = PredictionOutcome.Pending
        };
    }

    public void SetOutcome(MatchStatus status, int? actualHomeScore, int? actualAwayScore, IDateTimeProvider dateTimeProvider)
    {
        if (status == MatchStatus.Scheduled || actualHomeScore == null || actualAwayScore == null)
        {
            Outcome = PredictionOutcome.Pending;
            UpdatedAtUtc = dateTimeProvider.UtcNow;
            return;
        }

        if (PredictedHomeScore == actualHomeScore && PredictedAwayScore == actualAwayScore)
            Outcome = PredictionOutcome.ExactScore;
        else if (Math.Sign(PredictedHomeScore - PredictedAwayScore) == Math.Sign(actualHomeScore.Value - actualAwayScore.Value))
            Outcome = PredictionOutcome.CorrectResult;
        else
            Outcome = PredictionOutcome.Incorrect;

        UpdatedAtUtc = dateTimeProvider.UtcNow;
    }
}