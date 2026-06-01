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
        CostPayer payer = CostPayer.Business,
        string? notes = "Renews yearly")
        => RunningCost.Create(name, amount, frequency, _start, endDateUtc, payer, notes, _dateTimeProvider);

    [Fact]
    public void Create_ShouldSetPropertiesAndTrim()
    {
        var cost = RunningCost.Create("  Brevo  ", 9.99m, CostFrequency.Monthly, _start, null, CostPayer.Business, "  email  ", _dateTimeProvider);

        cost.Name.Should().Be("Brevo");
        cost.Amount.Should().Be(9.99m);
        cost.Frequency.Should().Be(CostFrequency.Monthly);
        cost.StartDateUtc.Should().Be(_start);
        cost.EndDateUtc.Should().BeNull();
        cost.Payer.Should().Be(CostPayer.Business);
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
        var act = () => RunningCost.Create("x", 10m, CostFrequency.Annual, default, null, CostPayer.Business, null, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenEndBeforeStart()
    {
        var act = () => RunningCost.Create("x", 10m, CostFrequency.Annual, _start, _start.AddDays(-1), CostPayer.Business, null, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldAllow_EndEqualToStart()
    {
        var act = () => RunningCost.Create("x", 10m, CostFrequency.Annual, _start, _start, CostPayer.Business, null, _dateTimeProvider);
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
    public void IsBusinessBorneOn_ShouldBeTrue_WhenPayerIsBusiness()
    {
        CreateCost(payer: CostPayer.Business).IsBusinessBorneOn(_dateTimeProvider.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsBusinessBorneOn_ShouldBeFalse_WhenPersonalWithNoEndDate()
    {
        CreateCost(payer: CostPayer.PersonalUntilRenewal, endDateUtc: null)
            .IsBusinessBorneOn(_dateTimeProvider.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsBusinessBorneOn_ShouldBeFalse_WhenPersonalAndRenewalInFuture()
    {
        CreateCost(payer: CostPayer.PersonalUntilRenewal, endDateUtc: _dateTimeProvider.UtcNow.AddMonths(1))
            .IsBusinessBorneOn(_dateTimeProvider.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsBusinessBorneOn_ShouldBeTrue_WhenPersonalAndRenewalReached()
    {
        CreateCost(payer: CostPayer.PersonalUntilRenewal, endDateUtc: _dateTimeProvider.UtcNow)
            .IsBusinessBorneOn(_dateTimeProvider.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldReplaceValues()
    {
        var cost = CreateCost();
        var newEnd = _start.AddYears(1);

        cost.Update("api-sports.io", 200m, CostFrequency.Annual, _start, newEnd, CostPayer.PersonalUntilRenewal, "fixtures");

        cost.Name.Should().Be("api-sports.io");
        cost.Amount.Should().Be(200m);
        cost.EndDateUtc.Should().Be(newEnd);
        cost.Payer.Should().Be(CostPayer.PersonalUntilRenewal);
        cost.Notes.Should().Be("fixtures");
    }

    [Fact]
    public void Update_ShouldNullBlankNotes()
    {
        var cost = CreateCost(notes: "original");

        cost.Update("api-sports.io", 200m, CostFrequency.Annual, _start, null, CostPayer.Business, "  ");

        cost.Notes.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldThrow_WhenInvalid()
    {
        var cost = CreateCost();
        var act = () => cost.Update("", 10m, CostFrequency.Annual, _start, null, CostPayer.Business, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabase()
    {
        var created = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var cost = new RunningCost(7, "Fasthosts", 120m, CostFrequency.Annual, _start, end, CostPayer.PersonalUntilRenewal, "note", created);

        cost.Id.Should().Be(7);
        cost.Name.Should().Be("Fasthosts");
        cost.EndDateUtc.Should().Be(end);
        cost.Payer.Should().Be(CostPayer.PersonalUntilRenewal);
        cost.CreatedAtUtc.Should().Be(created);
    }
}
