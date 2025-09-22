using Ardalis.GuardClauses;
using PredictionLeague.Domain.Services;

namespace PredictionLeague.Domain.Models;

public class League
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public int SeasonId { get; private set; }
    public decimal Price { get; private set; }
    public string AdministratorUserId { get; private set; } = string.Empty;
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
            CreatedAt = DateTime.Now
        };
    }

    private static void Validate(string name, DateTime entryDeadline, Season season)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        //Guard.Against.Expression(d => d <= DateTime.Now, entryDeadline, "Entry deadline must be in the future.");

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

    public async Task GenerateEntryCode(IEntryCodeUniquenessChecker uniquenessChecker, CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var isUnique = false;

        while (!isUnique)
        {
            EntryCode = new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
            isUnique = await uniquenessChecker.IsCodeUnique(EntryCode, cancellationToken);
        }
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

        if (EntryDeadline < DateTime.Now)
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
    
    public void RemoveMember(string userId)
    {
        var memberToRemove = _members.FirstOrDefault(m => m.UserId == userId);
        if (memberToRemove != null)
            _members.Remove(memberToRemove);
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

    public void ReassignAdministrator(string newAdministratorUserId)
    {
        Guard.Against.NullOrWhiteSpace(newAdministratorUserId, nameof(newAdministratorUserId));
        AdministratorUserId = newAdministratorUserId;
    }

    public List<LeagueMember> GetTopScorersForMatches(List<Match> matches)
    {
        if (!_members.Any() || !matches.Any())
            return [];

        var matchIds = matches.Select(m => m.Id).ToHashSet();

        var scores = new Dictionary<LeagueMember, int>();

        foreach (var member in _members)
        {
            var predictions = member.Predictions.Where(p => matchIds.Contains(p.MatchId)).DistinctBy(p => p.Id);
            var score = predictions.Sum(p => p.PointsAwarded ?? 0);
          
            scores.Add(member, score);
        }

        if (!scores.Any())
            return [];
        
        var maxScore = scores.Max(s => s.Value);
        if (maxScore == 0)
            return [];
        
        return scores
            .Where(s => s.Value == maxScore)
            .Select(s => s.Key)
            .ToList();
    }

    public List<OverallRanking> GetOverallRankings()
    {
        if (!_members.Any())
            return new List<OverallRanking>();

        var scoresByGroup = _members
            .GroupBy(m => m.Predictions.Sum(p => p.PointsAwarded ?? 0))
            .OrderByDescending(g => g.Key)
            .ToList();

        var rankings = new List<OverallRanking>();
        var currentRank = 1;

        foreach (var scoreGroup in scoresByGroup)
        {
            var membersInGroup = scoreGroup.ToList();
            rankings.Add(new OverallRanking(currentRank, membersInGroup));
            currentRank += membersInGroup.Count;
        }

        return rankings;
    }

    public List<LeagueMember> GetMostExactScoresWinners()
    {
        if (!_members.Any())
            return new List<LeagueMember>();
        
        var exactScoreCounts = _members
            .Select(member => new
            {
                Member = member,
                ExactCount = member.Predictions.Count(p => p.PointsAwarded == 5)
            }).ToList();

        if (!exactScoreCounts.Any())
            return new List<LeagueMember>();

        var maxCount = exactScoreCounts.Max(s => s.ExactCount);
        if (maxCount == 0)
            return new List<LeagueMember>();
        
        return exactScoreCounts
            .Where(s => s.ExactCount == maxCount)
            .Select(s => s.Member)
            .ToList();
    }

    #endregion
}