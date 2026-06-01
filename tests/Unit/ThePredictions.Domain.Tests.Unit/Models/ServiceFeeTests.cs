using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class ServiceFeeTests
{
    [Fact]
    public void CreateDefault_ShouldUseStripeDefaults_ForStripe()
    {
        var fee = ServiceFee.CreateDefault(ServiceFeeProvider.Stripe);

        fee.Provider.Should().Be(ServiceFeeProvider.Stripe);
        fee.PercentFee.Should().Be(ServiceFee.DefaultStripePercent);
        fee.FixedFee.Should().Be(ServiceFee.DefaultStripeFixedFee);
    }

    [Theory]
    [InlineData(ServiceFeeProvider.Sms)]
    [InlineData(ServiceFeeProvider.Email)]
    public void CreateDefault_ShouldBeZero_ForFlatRateProviders(ServiceFeeProvider provider)
    {
        var fee = ServiceFee.CreateDefault(provider);

        fee.Provider.Should().Be(provider);
        fee.PercentFee.Should().Be(0m);
        fee.FixedFee.Should().Be(0m);
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabase()
    {
        var fee = new ServiceFee(3, ServiceFeeProvider.Sms, 0m, 0.04m);

        fee.Id.Should().Be(3);
        fee.Provider.Should().Be(ServiceFeeProvider.Sms);
        fee.PercentFee.Should().Be(0m);
        fee.FixedFee.Should().Be(0.04m);
    }

    [Fact]
    public void Update_ShouldReplaceValues_WhenValid()
    {
        var fee = ServiceFee.CreateDefault(ServiceFeeProvider.Stripe);

        fee.Update(0.014m, 0.25m);

        fee.PercentFee.Should().Be(0.014m);
        fee.FixedFee.Should().Be(0.25m);
    }

    [Theory]
    [InlineData(1.0, 0.20)]    // percent out of range
    [InlineData(0.015, -0.1)]  // negative fixed fee
    public void Update_ShouldThrow_WhenInvalid(decimal percent, decimal fixedFee)
    {
        var fee = ServiceFee.CreateDefault(ServiceFeeProvider.Stripe);

        var act = () => fee.Update(percent, fixedFee);

        act.Should().Throw<ArgumentException>();
    }
}
