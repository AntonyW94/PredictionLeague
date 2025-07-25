﻿using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Domain.Models;

public class LeagueMember
{
    public int LeagueId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public LeagueMemberStatus Status { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    private readonly List<UserPrediction> _predictions = new();
    public IReadOnlyCollection<UserPrediction> Predictions => _predictions.AsReadOnly();

    private LeagueMember() { }

    public LeagueMember(
        int leagueId,
        string userId,
        LeagueMemberStatus status,
        DateTime joinedAt,
        DateTime? approvedAt,
        IEnumerable<UserPrediction?>? predictions)
    {
        LeagueId = leagueId;
        UserId = userId;
        Status = status;
        JoinedAt = joinedAt;
        ApprovedAt = approvedAt;

        if (predictions != null)
            _predictions.AddRange(predictions.Where(p => p != null).Select(p => p!));
    }

    public static LeagueMember Create(int leagueId, string userId)
    {
        Guard.Against.NegativeOrZero(leagueId, nameof(leagueId));
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));

        return new LeagueMember
        {
            LeagueId = leagueId,
            UserId = userId,
            Status = LeagueMemberStatus.Pending,
            JoinedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
    }
   
    public void Approve()
    {
        if (Status != LeagueMemberStatus.Pending)
            throw new InvalidOperationException("Only pending members can be approved.");

        Status = LeagueMemberStatus.Approved;
        ApprovedAt = DateTime.UtcNow;
    }
    
    public void Reject()
    {
        if (Status != LeagueMemberStatus.Pending)
            throw new InvalidOperationException("Only pending members can be rejected.");

        Status = LeagueMemberStatus.Rejected;
    }
    
    public void ScorePredictionForMatch(Match completedMatch)
    {
        Guard.Against.Null(completedMatch, nameof(completedMatch));

        var prediction = _predictions.FirstOrDefault(p => p.MatchId == completedMatch.Id);
        if (prediction != null && completedMatch.ActualHomeTeamScore.HasValue && completedMatch.ActualAwayTeamScore.HasValue)
            prediction.CalculatePoints(completedMatch.ActualHomeTeamScore.Value, completedMatch.ActualAwayTeamScore.Value);
    }
}