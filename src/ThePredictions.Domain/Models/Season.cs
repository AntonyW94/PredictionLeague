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
    public decimal? PassStandardPrice { get; private set; }
    public decimal? PassPremiumPrice { get; private set; }

    // A season requires a Season Pass when it has a price; free seasons have no prices.
    public bool RequiresPass => PassStandardPrice.HasValue;

    private Season() { }

    public Season(int id, string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId, decimal? passStandardPrice, decimal? passPremiumPrice)
    {
        Id = id;
        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive;
        NumberOfRounds = numberOfRounds;
        CompetitionId = competitionId;
        PassStandardPrice = passStandardPrice;
        PassPremiumPrice = passPremiumPrice;
    }

    public static Season Create(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId, decimal? passStandardPrice, decimal? passPremiumPrice)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds, competitionId, passStandardPrice, passPremiumPrice);

        var season = new Season
        {
            Name = name,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            IsActive = isActive,
            NumberOfRounds = numberOfRounds,
            CompetitionId = competitionId,
            PassStandardPrice = passStandardPrice,
            PassPremiumPrice = passPremiumPrice
        };

        return season;
    }

    public void UpdateDetails(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId, decimal? passStandardPrice, decimal? passPremiumPrice)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds, competitionId, passStandardPrice, passPremiumPrice);

        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive;
        NumberOfRounds = numberOfRounds;
        CompetitionId = competitionId;
        PassStandardPrice = passStandardPrice;
        PassPremiumPrice = passPremiumPrice;
    }

    public void SetIsActive(bool isActive)
    {
        IsActive = isActive;
    }

    private static void Validate(string name, DateTime startDateUtc, DateTime endDateUtc, int numberOfRounds, int competitionId, decimal? passStandardPrice, decimal? passPremiumPrice)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Default(startDateUtc);
        Guard.Against.Default(endDateUtc);
        Guard.Against.InvalidSeasonDuration(startDateUtc, endDateUtc);
        Guard.Against.OutOfRange(numberOfRounds, nameof(numberOfRounds), 1, 52);
        Guard.Against.NegativeOrZero(competitionId);
        ValidatePassPricing(passStandardPrice, passPremiumPrice);
    }

    private static void ValidatePassPricing(decimal? passStandardPrice, decimal? passPremiumPrice)
    {
        if (passStandardPrice is null && passPremiumPrice is null)
            return;

        if (passStandardPrice is null || passPremiumPrice is null)
            throw new ArgumentException("A paid season must have both a Standard price and a Premium price.", nameof(passStandardPrice));

        if (passStandardPrice <= 0)
            throw new ArgumentException("The Standard price must be greater than zero.", nameof(passStandardPrice));

        if (passPremiumPrice < passStandardPrice)
            throw new ArgumentException("The Premium price must be greater than or equal to the Standard price.", nameof(passPremiumPrice));
    }
}
