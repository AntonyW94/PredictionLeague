using FluentAssertions;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

public class EmailTemplateParametersTests
{
    [Fact]
    public void Extract_ShouldReturnDistinctNamesInDocumentOrder_WhenHtmlHasParams()
    {
        const string html = "<p>{{ params.FIRST_NAME }}</p><a href=\"{{ params.RESET_LINK }}\">x</a><p>{{ params.FIRST_NAME }}</p>";

        var result = EmailTemplateParameters.Extract(html);

        result.Should().Equal("FIRST_NAME", "RESET_LINK");
    }

    [Theory]
    [InlineData("{{ params.NAME }}")]
    [InlineData("{{params.NAME}}")]
    [InlineData("{{   params.NAME   }}")]
    public void Extract_ShouldTolerateWhitespaceVariations(string html)
    {
        var result = EmailTemplateParameters.Extract(html);

        result.Should().ContainSingle().Which.Should().Be("NAME");
    }

    [Fact]
    public void Extract_ShouldIgnoreNonParamMergeTags_WhenContactTagsPresent()
    {
        const string html = "{{ contact.EMAIL }} and {{ params.LEAGUE_NAME }}";

        var result = EmailTemplateParameters.Extract(html);

        result.Should().Equal("LEAGUE_NAME");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p>No merge tags here</p>")]
    public void Extract_ShouldReturnEmpty_WhenNoParamsPresent(string? html)
    {
        var result = EmailTemplateParameters.Extract(html);

        result.Should().BeEmpty();
    }
}
