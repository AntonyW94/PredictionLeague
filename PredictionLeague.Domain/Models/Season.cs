using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Domain.Models;

public class Season
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public DateTime StartDateUtc { get; private set; }
    public DateTime EndDateUtc { get; private set; }
    public bool IsActive { get; private set; }
    public int NumberOfRounds { get; private set; }
    public int? ApiLeagueId { get; private set; }

    private Season() { }

    public Season(int id, string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int? apiLeagueId)
    {
        Id = id;
        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive;
        NumberOfRounds = numberOfRounds;
        ApiLeagueId = apiLeagueId;
    }

    public static Season Create(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int? apiLeagueId)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds);

        var season = new Season
        {
            Name = name,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            IsActive = isActive,
            NumberOfRounds = numberOfRounds,
            ApiLeagueId = apiLeagueId
        };

        return season;
    }

    public void UpdateDetails(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int? apiLeagueId)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds);

        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive; 
        NumberOfRounds = numberOfRounds;
        ApiLeagueId = apiLeagueId;
    }

    public void SetIsActive(bool isActive)
    {
        IsActive = isActive;
    }

    private static void Validate(string name, DateTime startDateUtc, DateTime endDateUtc, int numberOfRounds)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Default(startDateUtc);
        Guard.Against.Default(endDateUtc);
        Guard.Against.InvalidSeasonDuration(startDateUtc, endDateUtc);
        Guard.Against.OutOfRange(numberOfRounds, nameof(numberOfRounds), 1, 52);
    }
}