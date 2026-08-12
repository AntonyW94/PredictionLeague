using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Guards.Season;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Models;

public class Season
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public DateTime StartDateUtc { get; private set; }
    public DateTime EndDateUtc { get; private set; }
    public bool IsActive { get; private set; }
    public int NumberOfRounds { get; private set; }

    /// <summary>
    /// Refuses to let the season hold more rounds than it says it has.
    /// </summary>
    /// <remarks>
    /// The declared number is not decoration: the prize scheme divides the pot by it, so a season quietly carrying a
    /// thirty-ninth round pays out thirty-eight rounds' worth of round prizes across thirty-nine rounds. It is also what
    /// "has this season finished" was decided from until the rounds that exist became the authority instead.
    ///
    /// Enforced rather than warned about, at the owner's decision. Raising the number on the season is the way to make
    /// room, which is a deliberate act with the prize consequences in view.
    /// </remarks>
    public void EnsureRoomForAnotherRound(int existingRoundCount)
    {
        if (existingRoundCount < NumberOfRounds)
            return;

        throw new BusinessRuleViolationException(
            $"{Name} already holds all {NumberOfRounds} rounds it declares. Raise the number of rounds on the season "
            + "before adding another, so the prize scheme is worked out from the right figure.");
    }
    public int CompetitionId { get; private set; }
    public decimal? PassStandardPrice { get; private set; }
    public decimal? PassPremiumPrice { get; private set; }

    // Every season requires a Season Pass to take part. A season requires *payment* only
    // when it has a price; "free" seasons have no prices, so the pass is acquired for £0.
    public bool RequiresPayment => PassStandardPrice.HasValue;

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
        // A free season has no prices. A paid season requires a Standard price; the
        // Premium (SMS) tier is optional and, when present, must not be cheaper than Standard.
        if (passStandardPrice is null && passPremiumPrice is null)
            return;

        if (passStandardPrice is null)
            throw new ArgumentException("A Premium price cannot be set without a Standard price.", nameof(passStandardPrice));

        if (passStandardPrice <= 0)
            throw new ArgumentException("The Standard price must be greater than zero.", nameof(passStandardPrice));

        if (passPremiumPrice is not null && passPremiumPrice < passStandardPrice)
            throw new ArgumentException("The Premium price must be greater than or equal to the Standard price.", nameof(passPremiumPrice));
    }
}
