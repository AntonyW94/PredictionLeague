using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Domain.Models;

public class Season
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public int NumberOfRounds { get; private set; }

    private Season() { }

    public static Season Create(string name, DateTime startDate, DateTime endDate, bool isActive, int numberOfRounds)
    {
        Validate(name, startDate, endDate, numberOfRounds);

        var season = new Season
        {
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = isActive,
            NumberOfRounds = numberOfRounds
        };

        return season;
    }

    public void UpdateDetails(string name, DateTime startDate, DateTime endDate, bool isActive, int numberOfRounds)
    {
        Validate(name, startDate, endDate, numberOfRounds);

        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive; 
        NumberOfRounds = numberOfRounds;
    }

    private static void Validate(string name, DateTime startDate, DateTime endDate, int numberOfRounds)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.Default(startDate, nameof(startDate));
        Guard.Against.Default(endDate, nameof(endDate));
        Guard.Against.InvalidSeasonDuration(startDate, endDate);
        Guard.Against.OutOfRange(numberOfRounds, nameof(numberOfRounds), 1, 52);
    }
}