using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class RunningCostTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));
    private readonly DateTime _start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private RunningCost CreateCost(
        string name = "Fasthosts hosting",
        decimal amount = 120m,
        CostFrequency frequency = CostFrequency.Annual,
        DateTime? endDateUtc = null,
        string? notes = "Renews yearly")
        => RunningCost.Create(name, amount, frequency, _start, endDateUtc, notes, _dateTimeProvider);

    [Fact]
    public void Create_ShouldSetPropertiesAndTrim()
    {
        var cost = RunningCost.Create("  Brevo  ", 9.99m, CostFrequency.Monthly, _start, null, "  email  ", _dateTimeProvider);

        cost.Name.Should().Be("Brevo");
        cost.Amount.Should().Be(9.99m);
        cost.Frequency.Should().Be(CostFrequency.Monthly);
        cost.StartDateUtc.Should().Be(_start);
        cost.EndDateUtc.Should().BeNull();
        cost.Notes.Should().Be("email");
        cost.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldNullBlankNotes()
    {
        CreateCost(notes: "   ").Notes.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameBlank()
    {
        var act = () => CreateCost(name: " ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountNegative()
    {
        var act = () => CreateCost(amount: -1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenStartDateDefault()
    {
        var act = () => RunningCost.Create("x", 10m, CostFrequency.Annual, default, null, null, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenEndBeforeStart()
    {
        var act = () => RunningCost.Create("x", 10m, CostFrequency.Annual, _start, _start.AddDays(-1), null, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldAllow_EndEqualToStart()
    {
        var act = () => RunningCost.Create("x", 10m, CostFrequency.Annual, _start, _start, null, _dateTimeProvider);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(CostFrequency.Monthly, 10, 120)]
    [InlineData(CostFrequency.Annual, 120, 120)]
    [InlineData(CostFrequency.OneOff, 50, 50)]
    public void AnnualisedAmount_ShouldConvertByFrequency(CostFrequency frequency, double amount, double expected)
    {
        CreateCost(amount: (decimal)amount, frequency: frequency).AnnualisedAmount.Should().Be((decimal)expected);
    }

    [Fact]
    public void Update_ShouldReplaceValues()
    {
        var cost = CreateCost();
        var newEnd = _start.AddYears(1);

        cost.Update("api-sports.io", 200m, CostFrequency.Annual, _start, newEnd, "fixtures");

        cost.Name.Should().Be("api-sports.io");
        cost.Amount.Should().Be(200m);
        cost.EndDateUtc.Should().Be(newEnd);
        cost.Notes.Should().Be("fixtures");
    }

    [Fact]
    public void Update_ShouldNullBlankNotes()
    {
        var cost = CreateCost(notes: "original");

        cost.Update("api-sports.io", 200m, CostFrequency.Annual, _start, null, "  ");

        cost.Notes.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldThrow_WhenInvalid()
    {
        var cost = CreateCost();
        var act = () => cost.Update("", 10m, CostFrequency.Annual, _start, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabase()
    {
        var created = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var cost = new RunningCost(7, "Fasthosts", 120m, CostFrequency.Annual, _start, end, "note", created);

        cost.Id.Should().Be(7);
        cost.Name.Should().Be("Fasthosts");
        cost.EndDateUtc.Should().Be(end);
        cost.CreatedAtUtc.Should().Be(created);
    }
}
