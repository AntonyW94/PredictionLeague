using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.EmailTests.Queries;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.EmailTests.Queries;

public class GetEmailTestTemplatesQueryHandlerTests
{
    private readonly IEmailTemplateCatalog _catalog = Substitute.For<IEmailTemplateCatalog>();
    private readonly GetEmailTestTemplatesQueryHandler _handler;

    public GetEmailTestTemplatesQueryHandlerTests()
    {
        _handler = new GetEmailTestTemplatesQueryHandler(_catalog);
    }

    [Fact]
    public async Task Handle_ShouldMapCatalogEntriesToDtos_WhenTemplatesExist()
    {
        var templates = new List<EmailTemplateInfo>
        {
            new(5, "League Join Approved", "You're in", true, ["FIRST_NAME", "LEAGUE_NAME"]),
            new(9, "Predictions Missing", "Don't forget", false, ["FIRST_NAME"])
        };
        _catalog.GetTemplatesAsync(CancellationToken.None).Returns(templates);

        var result = await _handler.Handle(new GetEmailTestTemplatesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(5);
        result[0].Name.Should().Be("League Join Approved");
        result[0].IsActive.Should().BeTrue();
        result[0].ParamNames.Should().Equal("FIRST_NAME", "LEAGUE_NAME");
        result[1].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenCatalogIsEmpty()
    {
        _catalog.GetTemplatesAsync(CancellationToken.None).Returns(new List<EmailTemplateInfo>());

        var result = await _handler.Handle(new GetEmailTestTemplatesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
