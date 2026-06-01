using FluentAssertions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class PricingSettingsTests
{
    [Fact]
    public void CreateDefault_ShouldUseDefaultConstants()
    {
        var settings = PricingSettings.CreateDefault();

        settings.BufferRate.Should().Be(PricingSettings.DefaultBufferRate);
        settings.StripePercent.Should().Be(PricingSettings.DefaultStripePercent);
        settings.StripeFixedFee.Should().Be(PricingSettings.DefaultStripeFixedFee);
        settings.MinimumFloor.Should().Be(PricingSettings.DefaultMinimumFloor);
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabase()
    {
        var settings = new PricingSettings(1, 0.20m, 0.014m, 0.25m, 1.50m);

        settings.Id.Should().Be(1);
        settings.BufferRate.Should().Be(0.20m);
        settings.StripePercent.Should().Be(0.014m);
        settings.StripeFixedFee.Should().Be(0.25m);
        settings.MinimumFloor.Should().Be(1.50m);
    }

    [Fact]
    public void Update_ShouldReplaceValues_WhenValid()
    {
        var settings = PricingSettings.CreateDefault();

        settings.Update(0.10m, 0.0125m, 0.30m, 2.00m);

        settings.BufferRate.Should().Be(0.10m);
        settings.StripePercent.Should().Be(0.0125m);
        settings.StripeFixedFee.Should().Be(0.30m);
        settings.MinimumFloor.Should().Be(2.00m);
    }

    [Theory]
    [InlineData(-0.1, 0.015, 0.20, 1.00)]   // negative buffer
    [InlineData(0.15, 1.0, 0.20, 1.00)]     // stripe percent out of range
    [InlineData(0.15, 0.015, -0.1, 1.00)]   // negative fixed fee
    [InlineData(0.15, 0.015, 0.20, -0.1)]   // negative floor
    public void Update_ShouldThrow_WhenInvalid(decimal buffer, decimal stripePercent, decimal stripeFixed, decimal floor)
    {
        var settings = PricingSettings.CreateDefault();

        var act = () => settings.Update(buffer, stripePercent, stripeFixed, floor);

        act.Should().Throw<ArgumentException>();
    }
}
