using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Models;

public partial class League
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public int SeasonId { get; private set; }
    public string AdministratorUserId { get; private set; } = string.Empty;
    public string? EntryCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime EntryDeadlineUtc { get; private set; }

    public int PointsForExactScore { get; private set; }
    public int PointsForCorrectResult { get; private set; }

    public decimal Price { get; private set; }
    public bool IsFree { get; private set; }
    public bool HasPrizes { get; private set; }
    public decimal? PrizeFundOverride { get; private set; }

    // When true, new joiners land as Pending and the admin must approve them. When false, joiners are
    // approved automatically. Toggleable by the admin at any time on the edit page.
    public bool RequiresMemberApproval { get; private set; }

    // When true, a private (entry-code) league is surfaced in the Available Leagues list so players can
    // discover it - but the entry code is still required to join. Public leagues are always discoverable
    // regardless of this flag.
    public bool IsListed { get; private set; }

    // Peer-to-peer entry-fee settlement. Bank fields hold ciphertext (encrypted at the command layer);
    // the platform never touches the money. PaymentReferenceTemplate is a non-sensitive display hint.
    public string? BankAccountName { get; private set; }
    public string? BankSortCode { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? PaymentReferenceTemplate { get; private set; }

    public bool HasBankDetails => BankAccountName is not null && BankSortCode is not null && BankAccountNumber is not null;

    public IReadOnlyCollection<LeagueMember> Members => _members.AsReadOnly();
    public IReadOnlyCollection<LeaguePrizeSetting> PrizeSettings => _prizeSettings.AsReadOnly();

    public LeaguePrizeScheme? PrizeScheme => _prizeScheme;

    private readonly List<LeagueMember> _members = new();
    private readonly List<LeaguePrizeSetting> _prizeSettings = new();
    private LeaguePrizeScheme? _prizeScheme;

    private League() { }

    public League(
        int id,
        string name,
        int seasonId,
        string administratorUserId,
        string? entryCode,
        DateTime createdAtUtc,
        DateTime entryDeadlineUtc,
        int pointsForExactScore,
        int pointsForCorrectResult,
        decimal price,
        bool isFree,             
        bool hasPrizes,          
        decimal? prizeFundOverride,
        IEnumerable<LeagueMember?>? members,
        IEnumerable<LeaguePrizeSetting?>? prizeSettings,
        string? bankAccountName = null,
        string? bankSortCode = null,
        string? bankAccountNumber = null,
        string? paymentReferenceTemplate = null,
        LeaguePrizeScheme? prizeScheme = null,
        bool requiresMemberApproval = true,
        bool isListed = false)
    {
        Id = id;
        Name = name;
        SeasonId = seasonId;
        AdministratorUserId = administratorUserId;
        EntryCode = entryCode;
        CreatedAtUtc = createdAtUtc;
        EntryDeadlineUtc = entryDeadlineUtc;
       
        PointsForExactScore = pointsForExactScore;
        PointsForCorrectResult = pointsForCorrectResult;

        Price = price;
        IsFree = isFree;
        HasPrizes = hasPrizes;
        PrizeFundOverride = prizeFundOverride;
        RequiresMemberApproval = requiresMemberApproval;
        IsListed = isListed;

        BankAccountName = bankAccountName;
        BankSortCode = bankSortCode;
        BankAccountNumber = bankAccountNumber;
        PaymentReferenceTemplate = paymentReferenceTemplate;

        if (members != null)
            _members.AddRange(members.Where(m => m != null).Select(m => m!));

        if (prizeSettings != null)
            _prizeSettings.AddRange(prizeSettings.Where(p => p != null).Select(p => p!));

        _prizeScheme = prizeScheme;
    }

    #region Factory Methods

    public static League Create(
        int seasonId,
        string name,
        string administratorUserId,
        DateTime entryDeadlineUtc,
        int pointsForExactScore,
        int pointsForCorrectResult,
        decimal price,
        Season season,
        IDateTimeProvider dateTimeProvider)
    {
        Validate(name, entryDeadlineUtc, season, dateTimeProvider);
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
            EntryDeadlineUtc = entryDeadlineUtc,
            CreatedAtUtc = dateTimeProvider.UtcNow,
            PointsForExactScore = pointsForExactScore,
            PointsForCorrectResult = pointsForCorrectResult,
            IsFree = isFree,
            HasPrizes = false,
            PrizeFundOverride = null,
            RequiresMemberApproval = true,
            IsListed = false
        };
    }

    /// <summary>
    /// Minimum gap between the entry deadline and the season start. Sized so the hourly
    /// freeze-prizes job always locks the prize scheme in before the first match kicks off.
    /// </summary>
    private const int MinimumHoursBetweenDeadlineAndSeasonStart = 6;

    private static void Validate(string name, DateTime entryDeadlineUtc, Season season, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Expression(d => d <= dateTimeProvider.UtcNow, entryDeadlineUtc, "Entry deadline must be in the future.");

        if (entryDeadlineUtc > season.StartDateUtc.AddHours(-MinimumHoursBetweenDeadlineAndSeasonStart))
            throw new ArgumentException($"Entry deadline must be at least {MinimumHoursBetweenDeadlineAndSeasonStart} hours before the season start date.", nameof(entryDeadlineUtc));
    }


    public static League CreateOfficialPublicLeague(int seasonId, string seasonName, decimal price, string administratorUserId, DateTime entryDeadlineUtc, Season season, IDateTimeProvider dateTimeProvider)
    {
        var league = Create(
            seasonId,
            $"{seasonName} Free League",
            administratorUserId,
            entryDeadlineUtc,
            PublicLeagueSettings.PointsForExactScore,
            PublicLeagueSettings.PointsForCorrectResult,
            price,
            season,
            dateTimeProvider
        );

        // The official league is open to all - joiners are approved automatically so the site admin
        // never has to action a backlog of join requests each season.
        league.RequiresMemberApproval = false;

        return league;
    }

    #endregion

    #region Business Logic Methods

    public void SetEntryCode(string entryCode)
    {
        Guard.Against.NullOrWhiteSpace(entryCode);

        if (!EntryCodePattern().IsMatch(entryCode))
            throw new ArgumentException("Entry code must be exactly 6 uppercase alphanumeric characters.", nameof(entryCode));

        EntryCode = entryCode;
    }

    [GeneratedRegex("^[A-Z0-9]{6}$")]
    private static partial Regex EntryCodePattern();

    public void UpdateDetails(
        string newName,
        decimal newPrice,
        DateTime newEntryDeadlineUtc,
        int newPointsForExactScore,
        int newPointsForCorrectResult,
        Season season,
        IDateTimeProvider dateTimeProvider)
    {
        Validate(newName, newEntryDeadlineUtc, season, dateTimeProvider);

        Name = newName;
        Price = newPrice;
        EntryDeadlineUtc = newEntryDeadlineUtc;
        PointsForExactScore = newPointsForExactScore;
        PointsForCorrectResult = newPointsForCorrectResult;
    }

    /// <summary>
    /// Sets the league's peer-to-peer entry-fee bank details. Bank values are expected to already be
    /// encrypted (the command layer encrypts before calling); the payment reference template is a plain hint.
    /// Pass nulls to clear the details and fall back to manual payment arrangement.
    /// </summary>
    public void SetBankDetails(string? bankAccountName, string? bankSortCode, string? bankAccountNumber, string? paymentReferenceTemplate)
    {
        BankAccountName = bankAccountName;
        BankSortCode = bankSortCode;
        BankAccountNumber = bankAccountNumber;
        PaymentReferenceTemplate = paymentReferenceTemplate;
    }

    public void AddMember(string userId, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        if (_members.Any(m => m.UserId == userId))
            throw new BusinessRuleViolationException("This user is already a member of the league.");

        if (EntryDeadlineUtc < dateTimeProvider.UtcNow)
            throw new BusinessRuleViolationException("The entry deadline for this league has passed.");

        var newMember = LeagueMember.Create(Id, userId, dateTimeProvider);

        // When the league does not require approval, the joiner is approved immediately so they can take
        // part straight away.
        if (!RequiresMemberApproval)
            newMember.Approve(dateTimeProvider);

        _members.Add(newMember);
    }

    /// <summary>
    /// Places a player in the league on a system administrator's behalf, approved straight away.
    /// </summary>
    /// <remarks>
    /// Deliberately ignores <see cref="EntryDeadlineUtc"/>, which is the whole reason this exists alongside
    /// <see cref="AddMember"/>. The deadline stops players entering themselves once the season is under way; it is not
    /// meant to catch somebody who paid in time and could not finish joining on their own device. Only an administrator
    /// can reach this, so the waiver is a decision somebody made rather than a hole in the rule.
    ///
    /// <see cref="RequiresMemberApproval"/> is skipped for the same reason: the administrator placing them has already
    /// made the approval decision, so leaving them Pending would only ask the league admin to confirm it again.
    /// </remarks>
    public void AddMemberAsAdministrator(string userId, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        if (_members.Any(m => m.UserId == userId))
            throw new BusinessRuleViolationException("This user is already a member of the league.");

        var newMember = LeagueMember.Create(Id, userId, dateTimeProvider);
        newMember.Approve(dateTimeProvider);

        _members.Add(newMember);
    }

    /// <summary>
    /// Toggles whether new join requests require admin approval. Turning approval off auto-approves any
    /// members currently awaiting approval and returns their user ids so callers can notify them.
    /// </summary>
    public IReadOnlyCollection<string> SetRequiresMemberApproval(bool requiresApproval, IDateTimeProvider dateTimeProvider)
    {
        RequiresMemberApproval = requiresApproval;

        if (requiresApproval)
            return [];

        var autoApprovedUserIds = new List<string>();
        foreach (var member in _members.Where(m => m.Status == LeagueMemberStatus.Pending))
        {
            member.Approve(dateTimeProvider);
            autoApprovedUserIds.Add(member.UserId);
        }

        return autoApprovedUserIds;
    }

    /// <summary>
    /// Sets whether a private league is surfaced in the Available Leagues list. The entry code is still
    /// required to join; this only controls discoverability.
    /// </summary>
    public void SetIsListed(bool isListed)
    {
        IsListed = isListed;
    }

    public void RemoveMember(string userId)
    {
        var memberToRemove = _members.FirstOrDefault(m => m.UserId == userId);
        if (memberToRemove != null)
            _members.Remove(memberToRemove);
    }

    /// <summary>
    /// What the league has taken in entry fees: the stake multiplied by the members who have joined.
    /// Provisional until the entry deadline, because the count can still change.
    /// </summary>
    public decimal TotalPrizePot => Price * _members.Count;

    /// <summary>
    /// The site-admin manual override for the prize structure. ADR-0011 superseded this with the
    /// scheme freeze, and it is kept only for edge cases - which is exactly why its two rules live
    /// here rather than in the caller: a rarely-walked path is the one most likely to be reimplemented
    /// without them.
    /// </summary>
    /// <param name="totalAllocated">
    /// What the caller is handing out in total. Passed in rather than summed from
    /// <paramref name="prizes"/> because a single setting can be awarded many times over (ten round
    /// prizes at the same amount are one setting), and that multiplier does not survive onto the
    /// entity. The league owns the pot; the caller owns the arithmetic of its own request.
    /// </param>
    public void RedefinePrizeStructure(IEnumerable<LeaguePrizeSetting> prizes, decimal totalAllocated, IDateTimeProvider dateTimeProvider)
    {
        // Before the deadline the entrant count, and so the pot, is not final - there is nothing
        // stable to divide up yet.
        if (EntryDeadlineUtc > dateTimeProvider.UtcNow)
            throw new BusinessRuleViolationException("The prize structure cannot be defined until after the entry deadline has passed.");

        if (totalAllocated != TotalPrizePot)
            throw new BusinessRuleViolationException("The total allocated prize money must equal the total prize pot.");

        DefinePrizes(prizes);
    }

    /// <summary>
    /// Replaces the prize settings outright. Unguarded on purpose: the automated freeze
    /// (PrizeSchemeFreezeService) runs at the deadline and derives the amounts from the scheme, so it
    /// has already satisfied the rules that <see cref="RedefinePrizeStructure"/> enforces for the
    /// manual path. Prefer that method for anything a person triggers.
    /// </summary>
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

    /// <summary>
    /// Sets the prize scheme once. Throws if a scheme is already set - league admins configure it
    /// at creation (or once on a schemeless league) and it locks thereafter. Site-admin corrections
    /// go through <see cref="OverridePrizeScheme"/>.
    /// </summary>
    public void SetPrizeScheme(LeaguePrizeScheme scheme)
    {
        Guard.Against.Null(scheme);

        if (_prizeScheme is not null)
            throw new BusinessRuleViolationException("The prize scheme has already been set for this league.");

        ApplyPrizeScheme(scheme);
    }

    /// <summary>
    /// Replaces the prize scheme regardless of the write-once lock. Authorisation (site-admin only)
    /// is enforced in the command handler, not here.
    /// </summary>
    public void OverridePrizeScheme(LeaguePrizeScheme scheme)
    {
        Guard.Against.Null(scheme);
        ApplyPrizeScheme(scheme);
    }

    private void ApplyPrizeScheme(LeaguePrizeScheme scheme)
    {
        _prizeScheme = scheme;

        // Free leagues with no admin top-up (PrizeFundOverride) are informational only - no prizes.
        HasPrizes = Price > 0 || (PrizeFundOverride ?? 0) > 0;
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

    /// <summary>
    /// Ranks members by their aggregate score across a specific set of rounds (a tournament stage),
    /// grouping ties at the same rank - the same tie semantics as <see cref="GetOverallRankings"/>.
    /// </summary>
    public List<OverallRanking> GetStageRankings(IEnumerable<int> roundIdsInStage)
    {
        var stageRoundIds = roundIdsInStage.ToHashSet();

        if (!_members.Any() || stageRoundIds.Count == 0)
            return new List<OverallRanking>();

        var scoresByGroup = _members
            .Select(m => new
            {
                Member = m,
                TotalScore = m.RoundResults.Where(r => stageRoundIds.Contains(r.RoundId)).Sum(r => r.BoostedPoints)
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

        var exactScoreCounts = _members.Select(member => new { Member = member, ExactCount = member.RoundResults.Sum(r => r.ExactScoreCount) }).ToList();

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