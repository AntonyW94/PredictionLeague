using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class UserPrediction
{
    [ExcludeFromCodeCoverage(Justification = "Set only by Dapper when hydrating from the database; the only constructor is private, so nothing else can reach it.")]
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
        Outcome = DetermineOutcome(status, actualHomeScore, actualAwayScore);
        UpdatedAtUtc = dateTimeProvider.UtcNow;
    }

    private PredictionOutcome DetermineOutcome(MatchStatus status, int? actualHomeScore, int? actualAwayScore)
    {
        if (!HasFinalScore(status, actualHomeScore, actualAwayScore))
            return PredictionOutcome.Pending;

        if (PredictedHomeScore == actualHomeScore && PredictedAwayScore == actualAwayScore)
            return PredictionOutcome.ExactScore;

        if (Math.Sign(PredictedHomeScore - PredictedAwayScore) == Math.Sign(actualHomeScore!.Value - actualAwayScore!.Value))
            return PredictionOutcome.CorrectResult;

        return PredictionOutcome.Incorrect;
    }

    private static bool HasFinalScore(MatchStatus status, int? actualHomeScore, int? actualAwayScore)
    {
        return status is not (MatchStatus.Scheduled or MatchStatus.Postponed)
               && actualHomeScore.HasValue
               && actualAwayScore.HasValue;
    }
}