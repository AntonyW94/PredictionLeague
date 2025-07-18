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

    private Season() { }

    public static Season Create(string name, DateTime startDate, DateTime endDate, bool isActive)
    {
        Validate(name, startDate, endDate);

        var season = new Season
        {
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = isActive
        };

        return season;
    }

    public void UpdateDetails(string name, DateTime startDate, DateTime endDate, bool isActive)
    {
        Validate(name, startDate, endDate);

        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive;
    }

    private static void Validate(string name, DateTime startDate, DateTime endDate)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.Default(startDate, nameof(startDate));
        Guard.Against.Default(endDate, nameof(endDate));
        Guard.Against.InvalidSeasonDuration(startDate, endDate);
    }
}