using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class League
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SeasonId { get; private set; }
    public string AdministratorUserId { get; private init; } = string.Empty;
    public string? EntryCode { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime EntryDeadline { get; private set; }

    private readonly List<LeagueMember> _members = new();
    public IReadOnlyCollection<LeagueMember> Members => _members.AsReadOnly();

    private League() { }

    public League(int id, string name, int seasonId, string administratorUserId, string? entryCode, DateTime createdAt, DateTime entryDeadline, IEnumerable<LeagueMember?>? members)
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

    #region Factory Methods

    public static League Create(
        int seasonId,
        string name,
        string administratorUserId,
        string? entryCode,
        DateTime entryDeadline)
    {
        Guard.Against.NegativeOrZero(seasonId, nameof(seasonId));
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(administratorUserId, nameof(administratorUserId));
        Guard.Against.Expression(d => d <= DateTime.UtcNow, entryDeadline, "Entry deadline must be in the future.");

        if (entryCode != null && entryCode.Length != 6)
            throw new ArgumentException("Private league entry code must be 6 characters.", nameof(entryCode));

        var league = new League
        {
            SeasonId = seasonId,
            Name = name,
            AdministratorUserId = administratorUserId,
            EntryCode = entryCode,
            EntryDeadline = entryDeadline,
            CreatedAt = DateTime.UtcNow
        };

        league.AddMember(administratorUserId);
        league.ApproveMember(administratorUserId, administratorUserId);

        return league;
    }


    public static League CreateOfficialPublicLeague(int seasonId, string seasonName, string administratorUserId, DateTime entryDeadline)
    {
        var league = Create(
            seasonId,
            $"Official {seasonName} League",
            administratorUserId,
            null,
            entryDeadline
        );

        return league;
    }

    #endregion

    #region Business Logic Methods

    public void UpdateDetails(string newName, string? newEntryCode, DateTime newEntryDeadline)
    {
        Guard.Against.NullOrWhiteSpace(newName, nameof(newName));
        Guard.Against.Expression(d => d <= DateTime.UtcNow, newEntryDeadline, "Entry deadline must be in the future.");

        if (newEntryCode != null && newEntryCode.Length != 6)
            throw new ArgumentException("Private league entry code must be 6 characters.", nameof(newEntryCode));

        Name = newName;
        EntryCode = newEntryCode;
        EntryDeadline = newEntryDeadline;
    }

    public void AddMember(string userId)
    {
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));

        if (_members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("This user is already a member of the league.");

        if (EntryDeadline < DateTime.UtcNow)
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

    #endregion
}