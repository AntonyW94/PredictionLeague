using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Guards.Season;

namespace ThePredictions.Domain.Models;

public class Season
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public DateTime StartDateUtc { get; private set; }
    public DateTime EndDateUtc { get; private set; }
    public bool IsActive { get; private set; }
    public int NumberOfRounds { get; private set; }
    public int CompetitionId { get; private set; }

    private Season() { }

    public Season(int id, string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId)
    {
        Id = id;
        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive;
        NumberOfRounds = numberOfRounds;
        CompetitionId = competitionId;
    }

    public static Season Create(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds, competitionId);

        var season = new Season
        {
            Name = name,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            IsActive = isActive,
            NumberOfRounds = numberOfRounds,
            CompetitionId = competitionId
        };

        return season;
    }

    public void UpdateDetails(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds, competitionId);

        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive;
        NumberOfRounds = numberOfRounds;
        CompetitionId = competitionId;
    }

    public void SetIsActive(bool isActive)
    {
        IsActive = isActive;
    }

    private static void Validate(string name, DateTime startDateUtc, DateTime endDateUtc, int numberOfRounds, int competitionId)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Default(startDateUtc);
        Guard.Against.Default(endDateUtc);
        Guard.Against.InvalidSeasonDuration(startDateUtc, endDateUtc);
        Guard.Against.OutOfRange(numberOfRounds, nameof(numberOfRounds), 1, 52);
        Guard.Against.NegativeOrZero(competitionId);
    }
}
