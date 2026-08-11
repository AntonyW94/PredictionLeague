using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

/// <summary>
/// A website running cost (e.g. hosting, fixture API, SMS), used by the recommended-price calculator.
/// Stores start/end dates so costs can be apportioned/prorated flexibly (ADR 0006).
/// </summary>
public class RunningCost
{
    public int Id { get; init; }
    public string Name { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public CostFrequency Frequency { get; private set; }
    public DateTime StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Annual equivalent of the cost (one-off amounts are returned as-is; the calculator applies horizon logic).</summary>
    public decimal AnnualisedAmount => Annualise(Amount, Frequency);

    /// <summary>
    /// The same rule for an amount and a frequency held on their own, for the read paths that never build a cost.
    /// </summary>
    /// <remarks>
    /// The price recommendation used to reach this by constructing a <see cref="RunningCost"/> per row with an id of zero, a
    /// name of "cost" and two epoch dates, purely to read the property back off it. Fabricating an entity to borrow one line
    /// of arithmetic invents state that was never in the database.
    /// </remarks>
    public static decimal Annualise(decimal amount, CostFrequency frequency) => frequency switch
    {
        CostFrequency.Monthly => amount * 12,
        CostFrequency.Annual => amount,
        _ => amount
    };

    [ExcludeFromCodeCoverage(Justification = "Parameterless constructor for Dapper hydration: no logic to test.")]
    private RunningCost() { }

    public RunningCost(int id, string name, decimal amount, CostFrequency frequency, DateTime startDateUtc,
        DateTime? endDateUtc, string? notes, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Amount = amount;
        Frequency = frequency;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        Notes = notes;
        CreatedAtUtc = createdAtUtc;
    }

    public static RunningCost Create(string name, decimal amount, CostFrequency frequency, DateTime startDateUtc,
        DateTime? endDateUtc, string? notes, IDateTimeProvider dateTimeProvider)
    {
        Validate(name, amount, startDateUtc, endDateUtc);

        return new RunningCost
        {
            Name = name.Trim(),
            Amount = amount,
            Frequency = frequency,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    public void Update(string name, decimal amount, CostFrequency frequency, DateTime startDateUtc,
        DateTime? endDateUtc, string? notes)
    {
        Validate(name, amount, startDateUtc, endDateUtc);

        Name = name.Trim();
        Amount = amount;
        Frequency = frequency;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
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
