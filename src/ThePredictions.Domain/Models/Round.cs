using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

public class Round
{
    public int Id { get; init; }
    public int SeasonId { get; private init; }
    public int RoundNumber { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public DateTime StartDateUtc { get; private set; }
    public DateTime DeadlineUtc { get; private set; }
    public DateTime? CompletedDateUtc { get; private set; }
    public RoundStatus Status { get; private set; }
    public string? ApiRoundName { get; private set; }
    public DateTime? LastReminderSentUtc { get; private set; }
    public DateTime? ResultsDigestSentUtc { get; private set; }

    private readonly List<Match> _matches = new();
    public IReadOnlyCollection<Match> Matches => _matches.AsReadOnly();

    public bool HasConfirmedFixtures => _matches.Any(m => m.AreTeamsConfirmed);

    private Round() { }
  
    public Round(int id, int seasonId, int roundNumber, string displayName, DateTime startDateUtc, DateTime deadlineUtc, RoundStatus status, string? apiRoundName, DateTime? lastReminderSentUtc, IEnumerable<Match?>? matches, DateTime? resultsDigestSentUtc = null)
    {
        Id = id;
        SeasonId = seasonId;
        RoundNumber = roundNumber;
        DisplayName = displayName;
        StartDateUtc = startDateUtc;
        DeadlineUtc = deadlineUtc;
        Status = status;
        ApiRoundName = apiRoundName;
        LastReminderSentUtc = lastReminderSentUtc;
        ResultsDigestSentUtc = resultsDigestSentUtc;

        if (matches != null)
            _matches.AddRange(matches.Where(m => m != null).Select(m => (Match)m!));
    }
  
    public static Round Create(int seasonId, int roundNumber, string displayName, DateTime startDateUtc, DateTime deadlineUtc, string? apiRoundName)
    {
        Validate(seasonId, roundNumber, displayName, startDateUtc, deadlineUtc);

        return new Round
        {
            SeasonId = seasonId,
            RoundNumber = roundNumber,
            DisplayName = displayName,
            StartDateUtc = startDateUtc,
            DeadlineUtc = deadlineUtc,
            Status = RoundStatus.Draft,
            ApiRoundName = apiRoundName,
            LastReminderSentUtc = null
        };
    }

    public void UpdateDetails(int roundNumber, string displayName, DateTime startDateUtc, DateTime deadlineUtc, RoundStatus status, string? apiRoundName)
    {
        Validate(SeasonId, roundNumber, displayName, startDateUtc, deadlineUtc);

        RoundNumber = roundNumber;
        DisplayName = displayName;
        StartDateUtc = startDateUtc;
        DeadlineUtc = deadlineUtc;
        Status = status;
        ApiRoundName = apiRoundName;
    }

    public void UpdateLastReminderSent(IDateTimeProvider dateTimeProvider)
    {
        LastReminderSentUtc = dateTimeProvider.UtcNow;
    }

    public void MarkResultsDigestSent(IDateTimeProvider dateTimeProvider)
    {
        ResultsDigestSentUtc = dateTimeProvider.UtcNow;
    }

    public void UpdateStatus(RoundStatus status, IDateTimeProvider dateTimeProvider)
    {
        var originalStatus = Status;

        Status = status;

        if (originalStatus != RoundStatus.Completed && status == RoundStatus.Completed)
            CompletedDateUtc = dateTimeProvider.UtcNow;
        else if (originalStatus == RoundStatus.Completed && status != RoundStatus.Completed)
            CompletedDateUtc = null;
    }

    public void AddMatch(int homeTeamId, int awayTeamId, DateTime matchTimeUtc, int? externalId)
    {
        var matchExists = _matches.Any(m => m.HomeTeamId == homeTeamId && m.AwayTeamId == awayTeamId);

        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");
        Guard.Against.Expression(m => m, matchExists, "This match already exists in the round.");

        _matches.Add(Match.Create(Id, homeTeamId, awayTeamId, matchTimeUtc, externalId));
    }

    public void AddPlaceholderMatch(string placeholderHomeName, string placeholderAwayName, string apiRoundName, int? matchNumber = null)
    {
        _matches.Add(Match.CreatePlaceholder(Id, placeholderHomeName, placeholderAwayName, apiRoundName, matchNumber));
    }

    public void AcceptMatch(Match match)
    {
        var matchExists = _matches.Any(m => m.Id == match.Id);
        Guard.Against.Expression(m => m, matchExists, "This match already exists in the round.");

        match.MoveToRound(Id);
        _matches.Add(match);
    }

    public void RemoveMatch(int matchId)
    {
        var matchToRemove = _matches.FirstOrDefault(m => m.Id == matchId);
        if (matchToRemove != null)
            _matches.Remove(matchToRemove);
    }

    /// <summary>
    /// The latest point at which any match in this round can still be predicted. Each match uses its own
    /// effective deadline (a per-match <see cref="Match.CustomLockTimeUtc"/> when set, otherwise the round
    /// deadline), so this is the round deadline unless a match carries a later custom lock. This lets a
    /// combined round (for example World Cup semi-finals plus the final and third-place playoff) stay open
    /// for the later matches after the round deadline that locked the earlier ones has passed.
    /// </summary>
    public DateTime GetLatestPredictionDeadline()
    {
        var latest = DeadlineUtc;

        foreach (var match in _matches)
        {
            var effectiveDeadline = match.GetEffectiveDeadline(DeadlineUtc);

            if (effectiveDeadline > latest)
                latest = effectiveDeadline;
        }

        return latest;
    }

    /// <summary>
    /// True once every match in the round has locked, i.e. no match can still be predicted. Use this rather
    /// than comparing against <see cref="DeadlineUtc"/> directly, which only reflects the earliest lock.
    /// </summary>
    public bool IsClosedForPredictions(DateTime utcNow)
    {
        return utcNow >= GetLatestPredictionDeadline();
    }

    /// <summary>
    /// The next moment a batch of this round's matches locks - the earliest effective deadline still in the
    /// future among confirmed, non-postponed matches - or null if every predictable match has already
    /// locked. Reminder scheduling keys its milestones off this so a combined round gets a fresh reminder
    /// wave before its later batch locks, rather than only before the round deadline.
    /// </summary>
    /// <summary>
    /// The round's name for display: its <see cref="DisplayName"/> when set, otherwise "Round N".
    /// </summary>
    /// <remarks>
    /// Written out in SQL as
    /// <c>CASE WHEN LEN(LTRIM(RTRIM(DisplayName))) > 0 THEN DisplayName ELSE 'Round ' + RoundNumber END</c>
    /// in both GetRoundCompletionQueryHandler and ReminderService - the second rule those two files
    /// duplicated, alongside the predictable-fixture predicate.
    /// </remarks>
    public string GetDisplayNameOrDefault() =>
        string.IsNullOrWhiteSpace(DisplayName) ? $"Round {RoundNumber}" : DisplayName;

    public DateTime? GetNextPredictionDeadline(DateTime utcNow)
    {
        DateTime? next = null;

        foreach (var match in _matches)
        {
            if (!match.AreTeamsConfirmed || match.Status == MatchStatus.Postponed)
                continue;

            var effectiveDeadline = match.GetEffectiveDeadline(DeadlineUtc);

            if (effectiveDeadline <= utcNow)
                continue;

            if (next == null || effectiveDeadline < next.Value)
                next = effectiveDeadline;
        }

        return next;
    }

    /// <summary>
    /// Recomputes the per-match custom lock times for this round from its confirmed matches. Matches whose
    /// teams are decided by the same earlier matches form a "batch" (see
    /// <see cref="TournamentRoundNameParser.GetPredictionBatch"/>) and lock together 30 minutes before the
    /// earliest kickoff in that batch - for example a combined round's final and third-place playoff, both
    /// decided by the semi-finals, lock together. The earliest batch carries no custom lock and uses the
    /// round deadline. The calculation depends only on each match's own batch and kickoff, never on when the
    /// previous batch is played, so batches whose predecessors kick off at different times still work.
    /// Idempotent: returns true only if at least one lock time actually changed.
    /// </summary>
    public bool RecalculateBatchPredictionLocks()
    {
        var batchedMatches = CollectPredictionBatches();
        if (batchedMatches.Count == 0)
            return false;

        return ApplyBatchLockTimes(batchedMatches);
    }

    /// <summary>
    /// Pairs each lockable match with its prediction batch. A match is not batchable until its teams
    /// are confirmed, and postponed matches plus any whose API round name does not parse to a known
    /// stage are skipped.
    /// </summary>
    private List<(Match Match, int Batch)> CollectPredictionBatches()
    {
        var batchedMatches = new List<(Match Match, int Batch)>();

        foreach (var match in _matches)
        {
            if (!match.AreTeamsConfirmed || match.Status == MatchStatus.Postponed || match.ApiRoundName == null)
                continue;

            if (!TournamentRoundNameParser.TryParseStage(match.ApiRoundName, out var stage))
                continue;

            batchedMatches.Add((match, TournamentRoundNameParser.GetPredictionBatch(stage)));
        }

        return batchedMatches;
    }

    /// <summary>
    /// The earliest batch uses the round deadline (no custom lock); later batches lock together 30
    /// minutes before the earliest kickoff among their own matches. Returns whether any lock moved.
    /// </summary>
    private static bool ApplyBatchLockTimes(List<(Match Match, int Batch)> batchedMatches)
    {
        var earliestBatch = batchedMatches.Min(b => b.Batch);
        var changed = false;

        foreach (var batch in batchedMatches.GroupBy(b => b.Batch))
        {
            DateTime? lockTimeUtc = batch.Key == earliestBatch
                ? null
                : batch.Min(b => b.Match.MatchDateTimeUtc).AddMinutes(-30);

            foreach (var (match, _) in batch)
            {
                if (match.CustomLockTimeUtc == lockTimeUtc)
                    continue;

                match.SetCustomLockTime(lockTimeUtc);
                changed = true;
            }
        }

        return changed;
    }

    private static void Validate(int seasonId, int roundNumber, string displayName, DateTime startDateUtc, DateTime deadlineUtc)
    {
        Guard.Against.NegativeOrZero(seasonId, "Season ID must be greater than 0");
        Guard.Against.NegativeOrZero(roundNumber, parameterName: null, message: "Round Number must be greater than 0");
        Guard.Against.NullOrWhiteSpace(displayName, message: "Please enter a Display Name");
        Guard.Against.Default(startDateUtc, "Please enter a Start Date");
        Guard.Against.Default(deadlineUtc, "Please enter a Deadline");
        Guard.Against.Expression(d => d >= startDateUtc, deadlineUtc, "Start date must be after the prediction deadline.");
    }
}