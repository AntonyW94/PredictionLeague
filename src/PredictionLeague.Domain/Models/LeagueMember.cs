using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Domain.Models;

public class LeagueMember
{
    public int LeagueId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public LeagueMemberStatus Status { get; private set; }
    public bool IsAlertDismissed { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public IReadOnlyCollection<LeagueRoundResult> RoundResults => _roundResults.AsReadOnly();

    private readonly List<LeagueRoundResult> _roundResults = new();

    private LeagueMember() { }

    public LeagueMember(
        int leagueId,
        string userId,
        LeagueMemberStatus status,
        bool isAlertDismissed,
        DateTime joinedAtUtc,
        DateTime? approvedAtUtc,
        IEnumerable<LeagueRoundResult>? roundResults)
    {
        LeagueId = leagueId;
        UserId = userId;
        Status = status;
        IsAlertDismissed = isAlertDismissed;
        JoinedAtUtc = joinedAtUtc;
        ApprovedAtUtc = approvedAtUtc;

        if (roundResults != null) 
            _roundResults.AddRange(roundResults);
    }

    public static LeagueMember Create(int leagueId, string userId, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NegativeOrZero(leagueId);
        Guard.Against.NullOrWhiteSpace(userId);

        return new LeagueMember
        {
            LeagueId = leagueId,
            UserId = userId,
            Status = LeagueMemberStatus.Pending,
            IsAlertDismissed = false,
            JoinedAtUtc = dateTimeProvider.UtcNow,
            ApprovedAtUtc = null
        };
    }

    public void Approve(IDateTimeProvider dateTimeProvider)
    {
        if (Status != LeagueMemberStatus.Pending)
            throw new InvalidOperationException("Only pending members can be approved.");

        Status = LeagueMemberStatus.Approved;
        ApprovedAtUtc = dateTimeProvider.UtcNow;
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