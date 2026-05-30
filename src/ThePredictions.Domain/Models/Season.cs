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
    public decimal? PassEntryPrice { get; private set; }
    public decimal? PassSmsPrice { get; private set; }

    // A season requires a Season Pass when it has a price; free seasons have no prices.
    public bool RequiresPass => PassEntryPrice.HasValue;

    private Season() { }

    public Season(int id, string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId, decimal? passEntryPrice, decimal? passSmsPrice)
    {
        Id = id;
        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive;
        NumberOfRounds = numberOfRounds;
        CompetitionId = competitionId;
        PassEntryPrice = passEntryPrice;
        PassSmsPrice = passSmsPrice;
    }

    public static Season Create(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId, decimal? passEntryPrice, decimal? passSmsPrice)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds, competitionId, passEntryPrice, passSmsPrice);

        var season = new Season
        {
            Name = name,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            IsActive = isActive,
            NumberOfRounds = numberOfRounds,
            CompetitionId = competitionId,
            PassEntryPrice = passEntryPrice,
            PassSmsPrice = passSmsPrice
        };

        return season;
    }

    public void UpdateDetails(string name, DateTime startDateUtc, DateTime endDateUtc, bool isActive, int numberOfRounds, int competitionId, decimal? passEntryPrice, decimal? passSmsPrice)
    {
        Validate(name, startDateUtc, endDateUtc, numberOfRounds, competitionId, passEntryPrice, passSmsPrice);

        Name = name;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = isActive;
        NumberOfRounds = numberOfRounds;
        CompetitionId = competitionId;
        PassEntryPrice = passEntryPrice;
        PassSmsPrice = passSmsPrice;
    }

    public void SetIsActive(bool isActive)
    {
        IsActive = isActive;
    }

    private static void Validate(string name, DateTime startDateUtc, DateTime endDateUtc, int numberOfRounds, int competitionId, decimal? passEntryPrice, decimal? passSmsPrice)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Default(startDateUtc);
        Guard.Against.Default(endDateUtc);
        Guard.Against.InvalidSeasonDuration(startDateUtc, endDateUtc);
        Guard.Against.OutOfRange(numberOfRounds, nameof(numberOfRounds), 1, 52);
        Guard.Against.NegativeOrZero(competitionId);
        ValidatePassPricing(passEntryPrice, passSmsPrice);
    }

    private static void ValidatePassPricing(decimal? passEntryPrice, decimal? passSmsPrice)
    {
        if (passEntryPrice is null && passSmsPrice is null)
            return;

        if (passEntryPrice is null || passSmsPrice is null)
            throw new ArgumentException("A paid season must have both an entry price and an SMS price.", nameof(passEntryPrice));

        if (passEntryPrice <= 0)
            throw new ArgumentException("The entry price must be greater than zero.", nameof(passEntryPrice));

        if (passSmsPrice < passEntryPrice)
            throw new ArgumentException("The SMS price must be greater than or equal to the entry price.", nameof(passSmsPrice));
    }
}
