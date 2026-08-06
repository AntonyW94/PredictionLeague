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
    public void Resolve_ShouldReturnHumanisedPlaceholder_ForUnknownNonLinkParams()
    {
        var result = _resolver.Resolve(["SOME_RANDOM_FIELD"], _user, "https://test.local");

        result["SOME_RANDOM_FIELD"].Should().Be("Some Random Field");
    }

    [Fact]
    public void Resolve_ShouldReturnOneEntryPerParam_PreservingNames()
    {
        var result = _resolver.Resolve(["FIRST_NAME", "LEAGUE_NAME", "ROUND_NAME", "DEADLINE"], _user, "https://test.local");

        result.Keys.Should().BeEquivalentTo("FIRST_NAME", "LEAGUE_NAME", "ROUND_NAME", "DEADLINE");
    }

    [Theory]
    [InlineData("LEAGUE_NAME", "Test League")]
    [InlineData("SEASON_NAME", "Test Season 2026")]
    [InlineData("ROUND_NAME", "Round 1")]
    [InlineData("NEXT_ROUND_NAME", "Round 2")]
    [InlineData("DEADLINE", "Saturday 14:30")]
    [InlineData("NEXT_ROUND_DEADLINE", "Saturday 14:30")]
    [InlineData("CORRECT_RESULTS", "5")]
    [InlineData("EXACT_SCORES", "2")]
    [InlineData("POINTS", "18")]
    [InlineData("TOP_SCORER", "Sarah J")]
    [InlineData("TOP_SCORER_POINTS", "24")]
    public void Resolve_ShouldSupplySampleContent_ForEveryFixedParam(string param, string expected)
    {
        var result = _resolver.Resolve([param], _user, "https://test.local");

        result[param].Should().Be(expected);
    }

    [Theory]
    [InlineData("CONFIRM_LINK", "https://test.local/authentication/confirm-email?token=TEST-TOKEN")]
    [InlineData("PREDICTIONS_URL", "https://test.local/predictions")]
    [InlineData("LEAGUE_URL", "https://test.local/leagues")]
    public void Resolve_ShouldBuildTheRemainingLinks_FromTheBaseUrl(string param, string expected)
    {
        var result = _resolver.Resolve([param], _user, "https://test.local");

        result[param].Should().Be(expected);
    }

    [Theory]
    [InlineData("first_name")]
    [InlineData("First_Name")]
    [InlineData("fIrSt_NaMe")]
    public void Resolve_ShouldMatchParamNamesRegardlessOfCase(string param)
    {
        var result = _resolver.Resolve([param], _user, "https://test.local");

        result[param].Should().Be("Antony");
    }

    [Fact]
    public void Resolve_ShouldKeyTheResultByTheNameAsSupplied()
    {
        // The caller substitutes on its own spelling of the name, so the key must come back
        // exactly as it went in even though matching is case-insensitive.
        var result = _resolver.Resolve(["first_name"], _user, "https://test.local");

        result.Keys.Should().ContainSingle().Which.Should().Be("first_name");
    }

    [Fact]
    public void Resolve_ShouldTrim_WhenTheUserHasNoLastName()
    {
        var result = _resolver.Resolve(["NAME"], new EmailTestUserData("Antony", "", "a@example.com"), "https://test.local");

        result["NAME"].Should().Be("Antony");
    }

    [Theory]
    [InlineData("SOME_RANDOM_FIELD", "Some Random Field")]
    [InlineData("ALREADY", "Already")]
    [InlineData("__DOUBLE__UNDERSCORE__", "Double Underscore")]
    [InlineData("MiXeD_CaSe", "Mixed Case")]
    public void Resolve_ShouldHumaniseAnUnrecognisedName(string param, string expected)
    {
        var result = _resolver.Resolve([param], _user, "https://test.local");

        result[param].Should().Be(expected);
    }

    [Fact]
    public void Resolve_ShouldReturnAnEmptyMap_WhenThereAreNoParams()
    {
        _resolver.Resolve([], _user, "https://test.local").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldNotDoubleUpTheSlash_WhenTheBaseUrlHasSeveralTrailingSlashes()
    {
        var result = _resolver.Resolve(["DASHBOARD_URL"], _user, "https://test.local///");

        result["DASHBOARD_URL"].Should().Be("https://test.local/dashboard");
    }
}
