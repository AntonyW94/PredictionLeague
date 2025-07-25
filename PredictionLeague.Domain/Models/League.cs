using Ardalis.GuardClauses;
using System.Text;

namespace PredictionLeague.Domain.Models;

public class League
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public int SeasonId { get; private set; }
    public decimal Price { get; private set; }
    public string AdministratorUserId { get; private init; } = string.Empty;
    public string? EntryCode { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime EntryDeadline { get; private set; }

    private readonly List<LeagueMember> _members = new();
    public IReadOnlyCollection<LeagueMember> Members => _members.AsReadOnly();

    private readonly List<LeaguePrizeSetting> _prizeSettings = new();
    public IReadOnlyCollection<LeaguePrizeSetting> PrizeSettings => _prizeSettings.AsReadOnly();

    private League() { }

    public League(
        int id,
        string name,
        decimal price,
        int seasonId,
        string administratorUserId,
        string? entryCode,
        DateTime createdAt,
        DateTime entryDeadline,
        IEnumerable<LeagueMember?>? members,
        IEnumerable<LeaguePrizeSetting?>? prizeSettings)
    {
        Id = id;
        Name = name;
        Price = price;
        SeasonId = seasonId;
        AdministratorUserId = administratorUserId;
        EntryCode = entryCode;
        CreatedAt = createdAt;
        EntryDeadline = entryDeadline;

        if (members != null)
            _members.AddRange(members.Where(m => m != null).Select(m => m!));

        if (prizeSettings != null)
            _prizeSettings.AddRange(prizeSettings.Where(p => p != null).Select(p => p!));
    }

    #region Factory Methods

    public static League Create(
        int seasonId,
        string name,
        decimal price,
        string administratorUserId,
        DateTime entryDeadline,
        Season season)
    {
        Validate(name, entryDeadline, season);
        Guard.Against.NullOrWhiteSpace(administratorUserId, nameof(administratorUserId));
        Guard.Against.NegativeOrZero(seasonId, nameof(seasonId));

        return new League
        {
            SeasonId = seasonId,
            Name = name,
            Price = price,
            AdministratorUserId = administratorUserId,
            EntryCode = null,
            EntryDeadline = entryDeadline,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static void Validate(string name, DateTime entryDeadline, Season season)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.Expression(d => d <= DateTime.UtcNow, entryDeadline, "Entry deadline must be in the future.");

        if (entryDeadline.Date >= season.StartDate.Date)
            throw new ArgumentException("Entry deadline must be at least one day before the season start date.", nameof(entryDeadline));
    }


    public static League CreateOfficialPublicLeague(int seasonId, string seasonName, decimal price, string administratorUserId, DateTime entryDeadline, Season season)
    {
        var league = Create(
            seasonId,
            $"Official {seasonName} League",
            price,
            administratorUserId,
            entryDeadline,
            season
        );

        return league;
    }

    #endregion

    #region Business Logic Methods

    public async Task GenerateEntryCode(Func<string, Task<bool>> isCodeUnique)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var codeBuilder = new StringBuilder(6);
        string newCode;

        do
        {
            codeBuilder.Clear();
            for (var i = 0; i < 6; i++)
            {
                codeBuilder.Append(chars[random.Next(chars.Length)]);
            }
            newCode = codeBuilder.ToString();
        }
        while (!await isCodeUnique(newCode));

        EntryCode = newCode;
    }

    public void UpdateDetails(string newName, decimal newPrice, DateTime newEntryDeadline, Season season)
    {
        Validate(newName, newEntryDeadline, season);

        Name = newName;
        Price = newPrice;
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

    public void RejectMember(string memberToRejectUserId, string rejecterUserId)
    {
        if (rejecterUserId != AdministratorUserId)
            throw new UnauthorizedAccessException("Only the league administrator can reject members.");

        var member = _members.FirstOrDefault(m => m.UserId == memberToRejectUserId);
        if (member == null)
            throw new KeyNotFoundException("Member not found in this league.");

        member.Reject();
    }

    public void ScoreMatch(Match completedMatch)
    {
        Guard.Against.Null(completedMatch, nameof(completedMatch));

        foreach (var member in _members)
        {
            member.ScorePredictionForMatch(completedMatch);
        }
    }

    public void DefinePrizes(IEnumerable<LeaguePrizeSetting> prizes)
    {
        _prizeSettings.Clear();
        _prizeSettings.AddRange(prizes);
    }

    #endregion
}