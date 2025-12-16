using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Domain.Models;

public class Round
{
    public int Id { get; init; }
    public int SeasonId { get; private init; }
    public int RoundNumber { get; private set; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; private set; }
    public RoundStatus Status { get; private set; }
    public string? ApiRoundName { get; private set; }
    public DateTime? LastReminderSent { get; private set; }

    private readonly List<Match> _matches = new();
    public IReadOnlyCollection<Match> Matches => _matches.AsReadOnly();

    private Round() { }
  
    public Round(int id, int seasonId, int roundNumber, DateTime startDate, DateTime deadline, RoundStatus status, string? apiRoundName, DateTime? lastReminderSent, IEnumerable<Match?>? matches)
    {
        Id = id;
        SeasonId = seasonId;
        RoundNumber = roundNumber;
        StartDate = startDate;
        Deadline = deadline;
        Status = status;
        ApiRoundName = apiRoundName;
        LastReminderSent = lastReminderSent;

        if (matches != null)
            _matches.AddRange(matches.Where(m => m != null).Select(m => (Match)m!));
    }
  
    public static Round Create(int seasonId, int roundNumber, DateTime startDate, DateTime deadline, string? apiRoundName)
    {
        Validate(seasonId, roundNumber, startDate, deadline);

        return new Round
        {
            SeasonId = seasonId,
            RoundNumber = roundNumber,
            StartDate = startDate,
            Deadline = deadline,
            Status = RoundStatus.Draft,
            ApiRoundName = apiRoundName,
            LastReminderSent = null
        };
    }

    public void UpdateDetails(int roundNumber, DateTime startDate, DateTime deadline, RoundStatus status, string? apiRoundName)
    {
        Validate(SeasonId, roundNumber, startDate, deadline);

        RoundNumber = roundNumber;
        StartDate = startDate;
        Deadline = deadline;
        Status = status;
        ApiRoundName = apiRoundName;
    }

    public void UpdateLastReminderSent()
    {
        LastReminderSent = DateTime.Now;
    }

    public void UpdateStatus(RoundStatus status)
    {
        Status = status;
    }

    public void AddMatch(int homeTeamId, int awayTeamId, DateTime matchTime, int? externalId)
    {
        var matchExists = _matches.Any(m => m.HomeTeamId == homeTeamId && m.AwayTeamId == awayTeamId);

        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");
        Guard.Against.Expression(m => m, matchExists, "This match already exists in the round.");

        _matches.Add(Match.Create(Id, homeTeamId, awayTeamId, matchTime, externalId));
    }

    public void RemoveMatch(int matchId)
    {
        var matchToRemove = _matches.FirstOrDefault(m => m.Id == matchId);
        if (matchToRemove != null)
            _matches.Remove(matchToRemove);
    }

    private static void Validate(int seasonId, int roundNumber, DateTime startDate, DateTime deadline)
    {
        Guard.Against.NegativeOrZero(seasonId, "Season ID must be greater than 0");
        Guard.Against.NegativeOrZero(roundNumber, parameterName: null, message: "Round Number must be greater than 0");
        Guard.Against.Default(startDate, "Please enter a Start Date");
        Guard.Against.Default(deadline, "Please enter a Deadline");
        Guard.Against.Expression(d => d >= startDate, deadline, "Start date must be after the prediction deadline.");
    }
}