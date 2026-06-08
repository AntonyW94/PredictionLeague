using FluentAssertions;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

public class EmailTestDefaultsResolverTests
{
    private readonly EmailTestDefaultsResolver _resolver = new();
    private readonly EmailTestUserData _user = new("Antony", "Willson", "antony@example.com");

    [Theory]
    [InlineData("FIRST_NAME", "Antony")]
    [InlineData("ADMIN_NAME", "Antony")]
    [InlineData("LAST_NAME", "Willson")]
    [InlineData("EMAIL", "antony@example.com")]
    [InlineData("NAME", "Antony Willson")]
    [InlineData("FULL_NAME", "Antony Willson")]
    public void Resolve_ShouldSeedFromUser_ForUserMatchedParams(string param, string expected)
    {
        var result = _resolver.Resolve([param], _user, "https://test.local");

        result[param].Should().Be(expected);
    }

    [Fact]
    public void Resolve_ShouldBuildLinks_FromBaseUrlWithoutDoubleSlash()
    {
        var result = _resolver.Resolve(["RESET_LINK", "DASHBOARD_URL", "LOGIN_LINK"], _user, "https://test.local/");

        result["RESET_LINK"].Should().Be("https://test.local/authentication/reset-password?token=TEST-TOKEN");
        result["DASHBOARD_URL"].Should().Be("https://test.local/dashboard");
        result["LOGIN_LINK"].Should().Be("https://test.local/authentication/login");
    }

    [Fact]
    public void Resolve_ShouldFallBackToBaseUrl_ForUnknownLinkParams()
    {
        var result = _resolver.Resolve(["INVITE_URL", "ACTIVATION_LINK"], _user, "https://test.local");

        result["INVITE_URL"].Should().Be("https://test.local");
        result["ACTIVATION_LINK"].Should().Be("https://test.local");
    }

    [Fact]
    public void Resolve_ShouldReturnEmptyString_ForUnknownNonLinkParams()
    {
        var result = _resolver.Resolve(["SOME_RANDOM_FIELD"], _user, "https://test.local");

        result["SOME_RANDOM_FIELD"].Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldReturnOneEntryPerParam_PreservingNames()
    {
        var result = _resolver.Resolve(["FIRST_NAME", "LEAGUE_NAME", "ROUND_NAME", "DEADLINE"], _user, "https://test.local");

        result.Keys.Should().BeEquivalentTo("FIRST_NAME", "LEAGUE_NAME", "ROUND_NAME", "DEADLINE");
    }
}
