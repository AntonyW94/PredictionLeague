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
        settings.MinimumFloor.Should().Be(PricingSettings.DefaultMinimumFloor);
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabase()
    {
        var settings = new PricingSettings(1, 0.20m, 1.50m);

        settings.Id.Should().Be(1);
        settings.BufferRate.Should().Be(0.20m);
        settings.MinimumFloor.Should().Be(1.50m);
    }

    [Fact]
    public void Update_ShouldReplaceValues_WhenValid()
    {
        var settings = PricingSettings.CreateDefault();

        settings.Update(0.10m, 2.00m);

        settings.BufferRate.Should().Be(0.10m);
        settings.MinimumFloor.Should().Be(2.00m);
    }

    [Theory]
    [InlineData(-0.1, 1.00)]   // negative buffer
    [InlineData(0.15, -0.1)]   // negative floor
    public void Update_ShouldThrow_WhenInvalid(decimal buffer, decimal floor)
    {
        var settings = PricingSettings.CreateDefault();

        var act = () => settings.Update(buffer, floor);

        act.Should().Throw<ArgumentException>();
    }
}
