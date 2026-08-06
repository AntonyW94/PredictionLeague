using ThePredictions.Contracts.Admin.RunningCosts;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Tests.Builders.Admin.RunningCosts;

public class SaveRunningCostRequestBuilder
{
    private string _name = "Web Hosting";
    private decimal _amount = 12.99m;
    private CostFrequency _frequency = CostFrequency.Monthly;
    private DateTime _startDateUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime? _endDateUtc;
    private string? _notes = "Paid annually in advance.";

    public SaveRunningCostRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public SaveRunningCostRequestBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public SaveRunningCostRequestBuilder WithFrequency(CostFrequency frequency)
    {
        _frequency = frequency;
        return this;
    }

    public SaveRunningCostRequestBuilder WithStartDateUtc(DateTime startDateUtc)
    {
        _startDateUtc = startDateUtc;
        return this;
    }

    public SaveRunningCostRequestBuilder WithEndDateUtc(DateTime? endDateUtc)
    {
        _endDateUtc = endDateUtc;
        return this;
    }

    public SaveRunningCostRequestBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public SaveRunningCostRequest Build() => new()
    {
        Name = _name,
        Amount = _amount,
        Frequency = _frequency,
        StartDateUtc = _startDateUtc,
        EndDateUtc = _endDateUtc,
        Notes = _notes
    };
}
