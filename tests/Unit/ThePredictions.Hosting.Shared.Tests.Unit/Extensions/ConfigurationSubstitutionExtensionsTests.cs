using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ThePredictions.Hosting.Shared.Extensions;
using Xunit;

namespace ThePredictions.Hosting.Shared.Tests.Unit.Extensions;

public class ConfigurationSubstitutionExtensionsTests
{
    private static IConfiguration Build(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void EnableSubstitutions_ShouldReplaceAPlaceholderWithTheReferencedValue()
    {
        var configuration = Build(
            ("BaseUrl", "https://example.com"),
            ("Callback", "${BaseUrl}/signin"));

        configuration.EnableSubstitutions();

        configuration["Callback"].Should().Be("https://example.com/signin");
    }

    [Fact]
    public void EnableSubstitutions_ShouldResolveAColonSeparatedSectionKey()
    {
        var configuration = Build(
            ("Api:Host", "api.example.com"),
            ("Api:Url", "https://${Api:Host}/v1"));

        configuration.EnableSubstitutions();

        configuration["Api:Url"].Should().Be("https://api.example.com/v1");
    }

    [Fact]
    public void EnableSubstitutions_ShouldResolveAKeyContainingAHyphen()
    {
        var configuration = Build(
            ("base-url", "https://example.com"),
            ("Callback", "${base-url}/done"));

        configuration.EnableSubstitutions();

        configuration["Callback"].Should().Be("https://example.com/done");
    }

    [Fact]
    public void EnableSubstitutions_ShouldResolveAChainOfPlaceholders()
    {
        var configuration = Build(
            ("Host", "example.com"),
            ("BaseUrl", "https://${Host}"),
            ("Callback", "${BaseUrl}/signin"));

        configuration.EnableSubstitutions();

        configuration["BaseUrl"].Should().Be("https://example.com");
        configuration["Callback"].Should().Be("https://example.com/signin");
    }

    [Fact]
    public void EnableSubstitutions_ShouldReplaceEveryPlaceholderInOneValue()
    {
        var configuration = Build(
            ("Scheme", "https"),
            ("Host", "example.com"),
            ("Url", "${Scheme}://${Host}/path"));

        configuration.EnableSubstitutions();

        configuration["Url"].Should().Be("https://example.com/path");
    }

    [Fact]
    public void EnableSubstitutions_ShouldLeaveAValueAlone_WhenItHasNoPlaceholder()
    {
        var configuration = Build(("Plain", "nothing to substitute"));

        configuration.EnableSubstitutions();

        configuration["Plain"].Should().Be("nothing to substitute");
    }

    [Fact]
    public void EnableSubstitutions_ShouldLeaveThePlaceholder_WhenTheReferencedKeyDoesNotExist()
    {
        var configuration = Build(("Callback", "${Missing}/signin"));

        configuration.EnableSubstitutions();

        configuration["Callback"].Should().Be("${Missing}/signin");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EnableSubstitutions_ShouldSkipBlankValues(string value)
    {
        var configuration = Build(("Blank", value), ("Other", "kept"));

        configuration.EnableSubstitutions();

        configuration["Blank"].Should().Be(value);
        configuration["Other"].Should().Be("kept");
    }

    [Fact]
    public void EnableSubstitutions_ShouldSkipNullValues()
    {
        var configuration = Build(("Nothing", null), ("Other", "kept"));

        configuration.EnableSubstitutions();

        configuration["Other"].Should().Be("kept");
    }

    [Fact]
    public void EnableSubstitutions_ShouldStopRatherThanLoopForever_WhenAValueReferencesItself()
    {
        var configuration = Build(("Self", "${Self}"));

        var act = () => configuration.EnableSubstitutions();

        act.Should().NotThrow();
        configuration["Self"].Should().Be("${Self}");
    }

    [Fact]
    public void EnableSubstitutions_ShouldStopRatherThanLoopForever_WhenTwoValuesReferenceEachOther()
    {
        var configuration = Build(
            ("A", "${B}"),
            ("B", "${A}"));

        var act = () => configuration.EnableSubstitutions();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnableSubstitutions_ShouldDoNothing_WhenThereIsNoConfiguration()
    {
        var configuration = Build();

        var act = () => configuration.EnableSubstitutions();

        act.Should().NotThrow();
    }
}
