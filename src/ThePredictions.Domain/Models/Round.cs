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

    private readonly List<Match> _matches = new();
    public IReadOnlyCollection<Match> Matches => _matches.AsReadOnly();

    public bool HasConfirmedFixtures => _matches.Any(m => m.AreTeamsConfirmed);

    private Round() { }
  
    public Round(int id, int seasonId, int roundNumber, string displayName, DateTime startDateUtc, DateTime deadlineUtc, RoundStatus status, string? apiRoundName, DateTime? lastReminderSentUtc, IEnumerable<Match?>? matches)
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