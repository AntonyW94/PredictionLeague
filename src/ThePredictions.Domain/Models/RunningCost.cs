using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

/// <summary>
/// A website running cost (e.g. hosting, fixture API, SMS), used by the recommended-price calculator.
/// Stores start/end dates and payer so costs can be apportioned/prorated flexibly (ADR 0006).
/// </summary>
public class RunningCost
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public CostFrequency Frequency { get; private set; }
    public DateTime StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public CostPayer Payer { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Annual equivalent of the cost (one-off amounts are returned as-is; the calculator applies horizon logic).</summary>
    public decimal AnnualisedAmount => Frequency switch
    {
        CostFrequency.Monthly => Amount * 12,
        CostFrequency.Annual => Amount,
        _ => Amount
    };

    /// <summary>
    /// Whether the business bears this cost as at the given date. Personal-until-renewal costs move to the
    /// business (and into pricing) on/after their end/renewal date.
    /// </summary>
    public bool IsBusinessBorneOn(DateTime dateUtc)
        => Payer == CostPayer.Business
           || (EndDateUtc.HasValue && EndDateUtc.Value <= dateUtc);

    [ExcludeFromCodeCoverage]
    private RunningCost() { }

    public RunningCost(int id, string name, decimal amount, CostFrequency frequency, DateTime startDateUtc,
        DateTime? endDateUtc, CostPayer payer, string? notes, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Amount = amount;
        Frequency = frequency;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        Payer = payer;
        Notes = notes;
        CreatedAtUtc = createdAtUtc;
    }

    public static RunningCost Create(string name, decimal amount, CostFrequency frequency, DateTime startDateUtc,
        DateTime? endDateUtc, CostPayer payer, string? notes, IDateTimeProvider dateTimeProvider)
    {
        Validate(name, amount, startDateUtc, endDateUtc);

        return new RunningCost
        {
            Name = name.Trim(),
            Amount = amount,
            Frequency = frequency,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            Payer = payer,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    public void Update(string name, decimal amount, CostFrequency frequency, DateTime startDateUtc,
        DateTime? endDateUtc, CostPayer payer, string? notes)
    {
        Validate(name, amount, startDateUtc, endDateUtc);

        Name = name.Trim();
        Amount = amount;
        Frequency = frequency;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        Payer = payer;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private static void Validate(string name, decimal amount, DateTime startDateUtc, DateTime? endDateUtc)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Negative(amount);
        Guard.Against.Default(startDateUtc);

        if (endDateUtc.HasValue && endDateUtc.Value < startDateUtc)
            throw new ArgumentException("End date must be on or after the start date.", nameof(endDateUtc));
    }
}
