using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Models;

public class Round
{
    public int Id { get; init; }
    public int SeasonId { get; private init; }
    public int RoundNumber { get; private set; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; private set; }

    private readonly List<Match> _matches = new();
    public IReadOnlyCollection<Match> Matches => _matches.AsReadOnly();

    private Round() { }
  
    public Round(int id, int seasonId, int roundNumber, DateTime startDate, DateTime deadline, IEnumerable<Match?>? matches)
    {
        Id = id;
        SeasonId = seasonId;
        RoundNumber = roundNumber;
        StartDate = startDate;
        Deadline = deadline;
    
        if (matches != null)
            _matches.AddRange(matches.Where(m => m != null).Select(m => (Match)m!));
    }
  
    public static Round Create(int seasonId, int roundNumber, DateTime startDate, DateTime deadline)
    {
        Validate(seasonId, roundNumber, startDate, deadline);

        return new Round
        {
            SeasonId = seasonId,
            RoundNumber = roundNumber,
            StartDate = startDate,
            Deadline = deadline
        };
    }

    public void UpdateDetails(int roundNumber, DateTime startDate, DateTime deadline)
    {
        Validate(SeasonId, roundNumber, startDate, deadline);

        RoundNumber = roundNumber;
        StartDate = startDate;
        Deadline = deadline;
    }
    
    public void AddMatch(int homeTeamId, int awayTeamId, DateTime matchTime)
    {
        var matchExists = _matches.Any(m => m.HomeTeamId == homeTeamId && m.AwayTeamId == awayTeamId);

        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");
        Guard.Against.Expression(m => m, matchExists, "This match already exists in the round.");

        _matches.Add(Match.Create(Id, homeTeamId, awayTeamId, matchTime));
    }

    public void ClearMatches()
    {
        _matches.Clear();
    }

    private static void Validate(int seasonId, int roundNumber, DateTime startDate, DateTime deadline)
    {
        Guard.Against.NegativeOrZero(seasonId, nameof(seasonId));
        Guard.Against.NegativeOrZero(roundNumber, nameof(roundNumber));
        Guard.Against.Default(startDate, nameof(startDate));
        Guard.Against.Default(deadline, nameof(deadline));
        Guard.Against.Expression(d => d <= startDate, deadline, "Start date must be before the prediction deadline.");
    }
}