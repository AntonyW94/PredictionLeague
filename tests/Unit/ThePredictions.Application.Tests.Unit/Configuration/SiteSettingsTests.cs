using FluentAssertions;
using ThePredictions.Application.Configuration;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Configuration;

public class SiteSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolvedBaseUrl_ShouldReturnFallback_WhenBaseUrlIsBlank(string? baseUrl)
    {
        var settings = new SiteSettings { BaseUrl = baseUrl };

        settings.ResolvedBaseUrl.Should().Be(SiteSettings.FallbackBaseUrl);
    }

    [Theory]
    [InlineData("https://dev.thepredictions.co.uk", "https://dev.thepredictions.co.uk")]
    [InlineData("https://dev.thepredictions.co.uk/", "https://dev.thepredictions.co.uk")]
    [InlineData("https://localhost:7132///", "https://localhost:7132")]
    public void ResolvedBaseUrl_ShouldReturnTrimmedConfiguredValue_WhenBaseUrlIsSet(string configured, string expected)
    {
        var settings = new SiteSettings { BaseUrl = configured };

        settings.ResolvedBaseUrl.Should().Be(expected);
    }
}
