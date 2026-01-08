using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Domain.Models;

public class LeagueMember
{
    public int LeagueId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public LeagueMemberStatus Status { get; private set; }
    public bool IsAlertDismissed { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public IReadOnlyCollection<LeagueRoundResult> RoundResults => _roundResults.AsReadOnly();

    private readonly List<LeagueRoundResult> _roundResults = new();

    private LeagueMember() { }

    public LeagueMember(
        int leagueId,
        string userId,
        LeagueMemberStatus status,
        bool isAlertDismissed,
        DateTime joinedAt,
        DateTime? approvedAt,
        IEnumerable<LeagueRoundResult>? roundResults)
    {
        LeagueId = leagueId;
        UserId = userId;
        Status = status;
        IsAlertDismissed = isAlertDismissed;
        JoinedAt = joinedAt;
        ApprovedAt = approvedAt;

        if (roundResults != null) 
            _roundResults.AddRange(roundResults);
    }

    public static LeagueMember Create(int leagueId, string userId)
    {
        Guard.Against.NegativeOrZero(leagueId);
        Guard.Against.NullOrWhiteSpace(userId);

        return new LeagueMember
        {
            LeagueId = leagueId,
            UserId = userId,
            Status = LeagueMemberStatus.Pending,
            IsAlertDismissed = false,
            JoinedAt = DateTime.Now,
            ApprovedAt = null
        };
    }

    public void Approve()
    {
        if (Status != LeagueMemberStatus.Pending)
            throw new InvalidOperationException("Only pending members can be approved.");

        Status = LeagueMemberStatus.Approved;
        ApprovedAt = DateTime.Now;
    }

    public void Reject()
    {
        if (Status != LeagueMemberStatus.Pending)
            throw new InvalidOperationException("Only pending members can be rejected.");

        Status = LeagueMemberStatus.Rejected;
        IsAlertDismissed = false;
    }

    public void DismissAlert()
    {
        IsAlertDismissed = true;
    }
}