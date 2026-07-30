using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Models;

public class LeagueMember
{
    public int LeagueId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public LeagueMemberStatus Status { get; private set; }
    public bool IsAlertDismissed { get; private set; }
    public bool IsArchivedByUser { get; private set; }
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
        bool isArchivedByUser,
        DateTime joinedAtUtc,
        DateTime? approvedAtUtc,
        IEnumerable<LeagueRoundResult>? roundResults)
    {
        LeagueId = leagueId;
        UserId = userId;
        Status = status;
        IsAlertDismissed = isAlertDismissed;
        IsArchivedByUser = isArchivedByUser;
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
            IsArchivedByUser = false,
            JoinedAtUtc = dateTimeProvider.UtcNow,
            ApprovedAtUtc = null
        };
    }

    public void Approve(IDateTimeProvider dateTimeProvider)
    {
        if (Status != LeagueMemberStatus.Pending)
            throw new BusinessRuleViolationException("Only pending members can be approved.");

        Status = LeagueMemberStatus.Approved;
        ApprovedAtUtc = dateTimeProvider.UtcNow;
    }

    public void Reject()
    {
        if (Status != LeagueMemberStatus.Pending)
            throw new BusinessRuleViolationException("Only pending members can be rejected.");

        Status = LeagueMemberStatus.Rejected;
        IsAlertDismissed = false;
    }

    public void DismissAlert()
    {
        IsAlertDismissed = true;
    }

    public void Archive()
    {
        if (Status != LeagueMemberStatus.Approved)
            throw new BusinessRuleViolationException("Only approved members can archive a league.");

        IsArchivedByUser = true;
    }

    public void Unarchive()
    {
        IsArchivedByUser = false;
    }
}