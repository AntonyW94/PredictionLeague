using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class League
{
    public int Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public int SeasonId { get; init; }
    public string AdministratorUserId { get; init; } = string.Empty;
    public string? EntryCode { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? EntryDeadline { get; init; }

    private readonly List<LeagueMember> _members = new();
    public IReadOnlyCollection<LeagueMember> Members => _members.AsReadOnly();

    private League() { }

    public League(int id, string name, int seasonId, string administratorUserId, string? entryCode, DateTime createdAt, DateTime? entryDeadline, IEnumerable<LeagueMember?>? members)
    {
        Id = id;
        Name = name;
        SeasonId = seasonId;
        AdministratorUserId = administratorUserId;
        EntryCode = entryCode;
        CreatedAt = createdAt;
        EntryDeadline = entryDeadline;

        if (members != null)
            _members.AddRange(members.Where(m => m != null).Select(m => (LeagueMember)m!));
    }

    public static League Create(
        int seasonId,
        string name,
        string administratorUserId,
        string? entryCode,
        DateTime? entryDeadline)
    {
        Guard.Against.NegativeOrZero(seasonId, nameof(seasonId));
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(administratorUserId, nameof(administratorUserId));

        if (entryCode != null && entryCode.Length != 6)
            throw new ArgumentException("Private league entry code must be 6 characters.", nameof(entryCode));
        
        if (entryDeadline.HasValue)
            Guard.Against.Expression(d => d <= DateTime.UtcNow, entryDeadline.Value, "Entry deadline must be in the future.");

        return new League
        {
            SeasonId = seasonId,
            Name = name,
            AdministratorUserId = administratorUserId,
            EntryCode = entryCode,
            EntryDeadline = entryDeadline,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddMember(string userId)
    {
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));

        if (_members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("This user is already a member of the league.");

        if (EntryDeadline.HasValue && EntryDeadline.Value < DateTime.UtcNow)
            throw new InvalidOperationException("The entry deadline for this league has passed.");

        var newMember = LeagueMember.Create(Id, userId);
        _members.Add(newMember);
    }

    public void ApproveMember(string memberToApproveUserId, string approverUserId)
    {
        if (approverUserId != AdministratorUserId)
            throw new UnauthorizedAccessException("Only the league administrator can approve new members.");
        
        var member = _members.FirstOrDefault(m => m.UserId == memberToApproveUserId);
        if (member == null)
            throw new KeyNotFoundException("Member not found in this league.");
        
        member.Approve();
    }

    public void ScoreMatch(Match completedMatch)
    {
        Guard.Against.Null(completedMatch, nameof(completedMatch));

        foreach (var member in _members)
        {
            member.ScorePredictionForMatch(completedMatch);
        }
    }
}