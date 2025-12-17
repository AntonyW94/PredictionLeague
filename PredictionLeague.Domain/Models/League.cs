using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Constants;
using PredictionLeague.Domain.Services;

namespace PredictionLeague.Domain.Models;

public class League
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public int SeasonId { get; private set; }
    public string AdministratorUserId { get; private set; } = string.Empty;
    public string? EntryCode { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime EntryDeadline { get; private set; }

    public int PointsForExactScore { get; private set; }
    public int PointsForCorrectResult { get; private set; }

    public decimal Price { get; private set; }
    public bool IsFree { get; private set; }
    public bool HasPrizes { get; private set; }
    public decimal? PrizeFundOverride { get; private set; }

    public IReadOnlyCollection<LeagueMember> Members => _members.AsReadOnly();
    public IReadOnlyCollection<LeaguePrizeSetting> PrizeSettings => _prizeSettings.AsReadOnly();

    private readonly List<LeagueMember> _members = new();
    private readonly List<LeaguePrizeSetting> _prizeSettings = new();

    private League() { }

    public League(
        int id,
        string name,
        int seasonId,
        string administratorUserId,
        string? entryCode,
        DateTime createdAt,
        DateTime entryDeadline,
        int pointsForExactScore,
        int pointsForCorrectResult,
        decimal price,
        bool isFree,             
        bool hasPrizes,          
        decimal? prizeFundOverride,
        IEnumerable<LeagueMember?>? members,
        IEnumerable<LeaguePrizeSetting?>? prizeSettings)
    {
        Id = id;
        Name = name;
        SeasonId = seasonId;
        AdministratorUserId = administratorUserId;
        EntryCode = entryCode;
        CreatedAt = createdAt;
        EntryDeadline = entryDeadline;
       
        PointsForExactScore = pointsForExactScore;
        PointsForCorrectResult = pointsForCorrectResult;

        Price = price;
        IsFree = isFree;
        HasPrizes = hasPrizes;
        PrizeFundOverride = prizeFundOverride;

        if (members != null)
            _members.AddRange(members.Where(m => m != null).Select(m => m!));

        if (prizeSettings != null)
            _prizeSettings.AddRange(prizeSettings.Where(p => p != null).Select(p => p!));
    }

    #region Factory Methods

    public static League Create(
        int seasonId,
        string name,
        string administratorUserId,
        DateTime entryDeadline,
        int pointsForExactScore,
        int pointsForCorrectResult,
        decimal price,
        Season season)
    {
        Validate(name, entryDeadline, season);
        Guard.Against.NullOrWhiteSpace(administratorUserId);
        Guard.Against.NegativeOrZero(seasonId);
        
        var isFree = price == 0;
       
        return new League
        {
            SeasonId = seasonId,
            Name = name,
            Price = price,
            AdministratorUserId = administratorUserId,
            EntryCode = null,
            EntryDeadline = entryDeadline,
            CreatedAt = DateTime.Now,
            PointsForExactScore = pointsForExactScore,
            PointsForCorrectResult = pointsForCorrectResult,
            IsFree = isFree,
            HasPrizes = false,
            PrizeFundOverride = null
        };
    }

    private static void Validate(string name, DateTime entryDeadline, Season season)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Expression(d => d <= DateTime.Now, entryDeadline, "Entry deadline must be in the future.");

        if (entryDeadline.Date >= season.StartDate.Date)
            throw new ArgumentException("Entry deadline must be at least one day before the season start date.", nameof(entryDeadline));
    }


    public static League CreateOfficialPublicLeague(int seasonId, string seasonName, decimal price, string administratorUserId, DateTime entryDeadline, Season season)
    {
        var league = Create(
            seasonId,
            $"Official {seasonName} League",
            administratorUserId,
            entryDeadline,
            PublicLeagueSettings.PointsForExactScore,
            PublicLeagueSettings.PointsForCorrectResult,
            price,
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
        Guard.Against.NullOrWhiteSpace(userId);

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

    public void DefinePrizes(IEnumerable<LeaguePrizeSetting>? prizes)
    {
        _prizeSettings.Clear();

        var prizesList = (prizes ?? []).ToList();
        if (prizesList.Any())
        {
            _prizeSettings.AddRange(prizesList);
            HasPrizes = true;
        }
        else
        {
            HasPrizes = false;
        }
    }

    public void SetPrizeFundOverride(decimal? amount)
    {
        PrizeFundOverride = amount;
    }

    public void ReassignAdministrator(string newAdministratorUserId)
    {
        Guard.Against.NullOrWhiteSpace(newAdministratorUserId);
        AdministratorUserId = newAdministratorUserId;
    }

    public List<LeagueMember> GetRoundWinners(int roundId)
    {
        if (!_members.Any())
            return [];

        var roundScores = _members.Select(m => new
        {
            Member = m,
            Score = m.RoundResults.FirstOrDefault(r => r.RoundId == roundId)?.BoostedPoints ?? 0
        }).ToList();

        var maxScore = roundScores.Max(s => s.Score);
        if (maxScore == 0)
            return [];

        return roundScores
            .Where(s => s.Score == maxScore)
            .Select(s => s.Member)
            .ToList();
    }

    public List<LeagueMember> GetPeriodWinners(IEnumerable<int> roundIdsInPeriod)
    {
        if (!_members.Any())
            return [];

        var targetRounds = roundIdsInPeriod.ToHashSet();

        var periodScores = _members.Select(m => new
        {
            Member = m,
            Score = m.RoundResults
                .Where(r => targetRounds.Contains(r.RoundId))
                .Sum(r => r.BoostedPoints)
        }).ToList();

        var maxScore = periodScores.Max(s => s.Score);
        if (maxScore == 0)
            return [];

        return periodScores
            .Where(s => s.Score == maxScore)
            .Select(s => s.Member)
            .ToList();
    }

    public List<OverallRanking> GetOverallRankings()
    {
        if (!_members.Any())
            return new List<OverallRanking>();

        var scoresByGroup = _members
            .Select(m => new
            {
                Member = m,
                TotalScore = m.RoundResults.Sum(r => r.BoostedPoints)
            })
            .GroupBy(x => x.TotalScore)
            .OrderByDescending(g => g.Key)
            .ToList();

        var rankings = new List<OverallRanking>();
        var currentRank = 1;

        foreach (var scoreGroup in scoresByGroup)
        {
            var membersInGroup = scoreGroup.Select(x => x.Member).ToList();
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
                ExactCount = member.RoundResults.Sum(r => r.ExactScoreCount)
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